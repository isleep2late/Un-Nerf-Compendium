namespace Isleep2late.Files.Services.Patch;

internal sealed class FgkDecoder : ISecondaryDecoder
{
	private const int AlphabetSize = 256;
	private const int TotalNodes = 2 * AlphabetSize - 1;
	private const int TotalBlocks = 2 * TotalNodes;
	private const int Null = -1;

	private readonly uint[] weight = new uint[TotalNodes];
	private readonly int[] parent = new int[TotalNodes];
	private readonly int[] leftChild = new int[TotalNodes];
	private readonly int[] rightChild = new int[TotalNodes];
	private readonly int[] left = new int[TotalNodes];
	private readonly int[] right = new int[TotalNodes];
	private readonly int[] myBlock = new int[TotalNodes];

	private readonly int[] block = new int[TotalBlocks];
	private readonly int[] codedBits = new int[AlphabetSize];

	private int freeBlock;
	private int zeroFreqCount;
	private int zeroFreqExp;
	private int zeroFreqRem;
	private int codedDepth;
	private int decodePtr;
	private int remainingZeros;
	private int rootNode;
	private int freeNode;

	internal static bool ProofSkipTreeUpdate { get; set; }

	public FgkDecoder()
	{
		rootNode = 0;
		decodePtr = rootNode;
		freeNode = AlphabetSize;
		remainingZeros = 0;
		codedDepth = 0;
		zeroFreqCount = AlphabetSize + 2;
		FactorRemaining();
		FactorRemaining();
		for (var b = 0; b < TotalBlocks - 1; b++)
			block[b] = b + 1;
		block[TotalBlocks - 1] = Null;
		freeBlock = 0;
		for (var n = 0; n < TotalNodes; n++)
		{
			parent[n] = leftChild[n] = rightChild[n] = left[n] = right[n] = myBlock[n] = Null;
			weight[n] = 0;
		}
		for (var i = AlphabetSize - 1; i >= 0; i--)
		{
			rightChild[i] = i < AlphabetSize - 1 ? i + 1 : Null;
			leftChild[i] = i >= 1 ? i - 1 : Null;
		}
	}

	public void Decode(ReadOnlySpan<byte> input, SectionBuffer output, int outputBytes, string where)
	{
		if (outputBytes > (long)input.Length * 8)
			throw PatchReader.Corrupt($"{where}: {input.Length:N0} byte(s) of FGK cannot hold the {outputBytes:N0} bytes the section declares; the patch is corrupt");
		var produced = 0;
		var position = 0;
		var first = true;
		while (true)
		{
			if (position == input.Length)
				throw PatchReader.Corrupt($"{where}: the FGK bitstream ends after {produced:N0} of the section's {outputBytes:N0} bytes; the patch is truncated or corrupt");
			int current = input[position++];
			for (var mask = 1; mask != 0x100; mask <<= 1)
			{
				if (!DecodeBit((current & mask) != 0 ? 1 : 0))
					continue;
				output.Add((byte)DecodeData(skipUpdate: ProofSkipTreeUpdate && !first));
				first = false;
				if (++produced == outputBytes)
				{
					if (position != input.Length)
						throw PatchReader.Corrupt($"{where}: the FGK decoder finished {input.Length - position:N0} byte(s) before the section did; the patch is corrupt");
					return;
				}
			}
		}
	}

	public void PassOver(ReadOnlySpan<byte> raw)
	{
		foreach (var b in raw)
			UpdateTree(b);
		decodePtr = rootNode;
	}

	private bool DecodeBit(int bit)
	{
		if (weight[decodePtr] == 0)
		{
			var required = zeroFreqRem == 0 ? zeroFreqExp : zeroFreqExp + 1;
			codedBits[codedDepth++] = bit;
			return codedDepth >= required;
		}
		decodePtr = bit != 0 ? rightChild[decodePtr] : leftChild[decodePtr];
		if (leftChild[decodePtr] == Null)
			return weight[decodePtr] != 0 || zeroFreqCount == 1;
		return false;
	}

	private int DecodeData(bool skipUpdate)
	{
		var element = decodePtr;
		if (weight[decodePtr] == 0)
		{
			var i = 0;
			var n = 0;
			if (codedDepth > 0)
			{
				for (; i < codedDepth - 1; i++)
				{
					n |= codedBits[i];
					n <<= 1;
				}
			}
			n |= codedBits[i];
			element = NthZero(n);
		}
		codedDepth = 0;
		if (!skipUpdate)
			UpdateTree(element);
		decodePtr = rootNode;
		return element;
	}

	private int NthZero(int n)
	{
		var node = remainingZeros;
		for (; n != 0 && rightChild[node] != Null; n--)
			node = rightChild[node];
		return node;
	}

	private void UpdateTree(int symbol)
	{
		var incr = weight[symbol] == 0 ? IncreaseZeroWeight(symbol) : symbol;
		while (incr != rootNode)
		{
			MoveRight(incr);
			Promote(incr);
			weight[incr]++;
			incr = parent[incr];
		}
		weight[rootNode]++;
	}

	private void MoveRight(int moveFwd)
	{
		var moveBack = block[myBlock[moveFwd]];
		if (moveFwd == moveBack || parent[moveFwd] == moveBack || weight[moveFwd] == 0)
			return;

		left[right[moveBack]] = moveFwd;
		if (left[moveFwd] != Null)
			right[left[moveFwd]] = moveBack;

		var tmp = right[moveFwd];
		right[moveFwd] = right[moveBack];
		if (tmp == moveBack)
			right[moveBack] = moveFwd;
		else
		{
			left[tmp] = moveBack;
			right[moveBack] = tmp;
		}

		tmp = left[moveBack];
		left[moveBack] = left[moveFwd];
		if (tmp == moveFwd)
			left[moveFwd] = moveBack;
		else
		{
			right[tmp] = moveFwd;
			left[moveFwd] = tmp;
		}

		var fwdParent = parent[moveFwd];
		var fwdIsRight = rightChild[fwdParent] == moveFwd;
		var backParent = parent[moveBack];
		var backIsRight = rightChild[backParent] == moveBack;
		parent[moveFwd] = backParent;
		parent[moveBack] = fwdParent;
		var fwdSlot = fwdIsRight ? rightChild[fwdParent] : leftChild[fwdParent];
		var backSlot = backIsRight ? rightChild[backParent] : leftChild[backParent];
		if (fwdIsRight)
			rightChild[fwdParent] = backSlot;
		else
			leftChild[fwdParent] = backSlot;
		if (backIsRight)
			rightChild[backParent] = fwdSlot;
		else
			leftChild[backParent] = fwdSlot;

		block[myBlock[moveFwd]] = moveFwd;
	}

	private void Promote(int node)
	{
		var myRight = right[node];
		var myLeft = left[node];
		var curBlock = myBlock[node];
		if (weight[node] == 0)
			return;

		if (myLeft == rightChild[node] && leftChild[node] != Null && weight[leftChild[node]] == 0)
		{
			if (weight[node] == weight[myRight] - 1 && myRight != rootNode)
			{
				FreeBlock(curBlock);
				myBlock[node] = myBlock[myRight];
				myBlock[myLeft] = myBlock[myRight];
			}
			return;
		}

		if (myLeft == remainingZeros)
			return;

		if (myBlock[myLeft] == curBlock)
			block[myBlock[myLeft]] = myLeft;
		else
			FreeBlock(curBlock);

		if (weight[node] == weight[myRight] - 1 && myRight != rootNode)
			myBlock[node] = myBlock[myRight];
		else
			myBlock[node] = MakeBlock(node);
	}

	private int IncreaseZeroWeight(int symbol)
	{
		var thisZero = symbol;
		if (zeroFreqCount == 1)
		{
			rightChild[thisZero] = Null;
			myBlock[thisZero] = weight[right[thisZero]] == 1 ? myBlock[right[thisZero]] : MakeBlock(thisZero);
			remainingZeros = Null;
			return thisZero;
		}

		var zeroPtr = remainingZeros;
		var newInternal = freeNode++;
		parent[newInternal] = parent[zeroPtr];
		right[newInternal] = right[zeroPtr];
		weight[newInternal] = 0;
		rightChild[newInternal] = thisZero;
		left[newInternal] = thisZero;

		if (remainingZeros == rootNode)
		{
			rootNode = newInternal;
			myBlock[thisZero] = MakeBlock(thisZero);
			myBlock[newInternal] = MakeBlock(newInternal);
		}
		else
		{
			left[right[newInternal]] = newInternal;
			if (rightChild[parent[zeroPtr]] == zeroPtr)
				rightChild[parent[zeroPtr]] = newInternal;
			else
				leftChild[parent[zeroPtr]] = newInternal;
			myBlock[newInternal] = weight[right[newInternal]] == 1 ? myBlock[right[newInternal]] : MakeBlock(newInternal);
			myBlock[thisZero] = myBlock[newInternal];
		}

		EliminateZero(thisZero);

		leftChild[newInternal] = remainingZeros;
		right[thisZero] = newInternal;
		left[thisZero] = remainingZeros;
		parent[thisZero] = newInternal;
		leftChild[thisZero] = Null;
		rightChild[thisZero] = Null;
		parent[remainingZeros] = newInternal;
		right[remainingZeros] = thisZero;
		return thisZero;
	}

	private void EliminateZero(int node)
	{
		if (zeroFreqCount == 1)
			return;
		FactorRemaining();
		if (leftChild[node] == Null)
		{
			remainingZeros = rightChild[remainingZeros];
			leftChild[remainingZeros] = Null;
		}
		else if (rightChild[node] == Null)
			rightChild[leftChild[node]] = Null;
		else
		{
			leftChild[rightChild[node]] = leftChild[node];
			rightChild[leftChild[node]] = rightChild[node];
		}
	}

	private int MakeBlock(int lead)
	{
		var b = freeBlock;
		freeBlock = block[freeBlock];
		block[b] = lead;
		return b;
	}

	private void FreeBlock(int b)
	{
		block[b] = freeBlock;
		freeBlock = b;
	}

	private void FactorRemaining()
	{
		var i = --zeroFreqCount;
		zeroFreqExp = 0;
		while (i > 1)
		{
			zeroFreqExp++;
			i >>= 1;
		}
		zeroFreqRem = zeroFreqCount - (1 << zeroFreqExp);
	}
}
