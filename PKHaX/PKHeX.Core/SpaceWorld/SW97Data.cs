using System;

namespace PKHeX.Core;

/// PKHaX: Space World '97 demo data, generated from the pret pokegold-spaceworld disassembly.
public static class SW97Data
{
    public const int MaxSpeciesID = 251;
    public const int MaxMoveID = 250;
    public const int TypeUnknown = 0x13;

    public static readonly string[] SpeciesNames =
    [
        "NONE", "BULBASAUR", "IVYSAUR", "VENUSAUR", "CHARMANDER", "CHARMELEON",
        "CHARIZARD", "SQUIRTLE", "WARTORTLE", "BLASTOISE", "CATERPIE", "METAPOD",
        "BUTTERFREE", "WEEDLE", "KAKUNA", "BEEDRILL", "PIDGEY", "PIDGEOTTO",
        "PIDGEOT", "RATTATA", "RATICATE", "SPEAROW", "FEAROW", "EKANS",
        "ARBOK", "PIKACHU", "RAICHU", "SANDSHREW", "SANDSLASH", "NIDORAN_F",
        "NIDORINA", "NIDOQUEEN", "NIDORAN_M", "NIDORINO", "NIDOKING", "CLEFAIRY",
        "CLEFABLE", "VULPIX", "NINETALES", "JIGGLYPUFF", "WIGGLYTUFF", "ZUBAT",
        "GOLBAT", "ODDISH", "GLOOM", "VILEPLUME", "PARAS", "PARASECT",
        "VENONAT", "VENOMOTH", "DIGLETT", "DUGTRIO", "MEOWTH", "PERSIAN",
        "PSYDUCK", "GOLDUCK", "MANKEY", "PRIMEAPE", "GROWLITHE", "ARCANINE",
        "POLIWAG", "POLIWHIRL", "POLIWRATH", "ABRA", "KADABRA", "ALAKAZAM",
        "MACHOP", "MACHOKE", "MACHAMP", "BELLSPROUT", "WEEPINBELL", "VICTREEBEL",
        "TENTACOOL", "TENTACRUEL", "GEODUDE", "GRAVELER", "GOLEM", "PONYTA",
        "RAPIDASH", "SLOWPOKE", "SLOWBRO", "MAGNEMITE", "MAGNETON", "FARFETCHD",
        "DODUO", "DODRIO", "SEEL", "DEWGONG", "GRIMER", "MUK",
        "SHELLDER", "CLOYSTER", "GASTLY", "HAUNTER", "GENGAR", "ONIX",
        "DROWZEE", "HYPNO", "KRABBY", "KINGLER", "VOLTORB", "ELECTRODE",
        "EXEGGCUTE", "EXEGGUTOR", "CUBONE", "MAROWAK", "HITMONLEE", "HITMONCHAN",
        "LICKITUNG", "KOFFING", "WEEZING", "RHYHORN", "RHYDON", "CHANSEY",
        "TANGELA", "KANGASKHAN", "HORSEA", "SEADRA", "GOLDEEN", "SEAKING",
        "STARYU", "STARMIE", "MRMIME", "SCYTHER", "JYNX", "ELECTABUZZ",
        "MAGMAR", "PINSIR", "TAUROS", "MAGIKARP", "GYARADOS", "LAPRAS",
        "DITTO", "EEVEE", "VAPOREON", "JOLTEON", "FLAREON", "PORYGON",
        "OMANYTE", "OMASTAR", "KABUTO", "KABUTOPS", "AERODACTYL", "SNORLAX",
        "ARTICUNO", "ZAPDOS", "MOLTRES", "DRATINI", "DRAGONAIR", "DRAGONITE",
        "MEWTWO", "MEW", "HAPPA", "HANAMOGURA", "HANARYU", "HONOGUMA",
        "VOLBEAR", "DYNABEAR", "KURUSU", "AQUA", "AQUARIA", "HOHO",
        "BOBO", "PACHIMEE", "MOKOKO", "DENRYU", "MIKON", "MONJA",
        "JARANRA", "HANEEI", "PUKU", "SHIBIREFUGU", "PICHU", "PY",
        "PUPURIN", "MIZUUO", "NATY", "NATIO", "GYOPIN", "MARIL",
        "MANBO1", "IKARI", "GROTESS", "EKSING", "PARA", "KOKUMO",
        "TWOHEAD", "YOROIDORI", "ANIMON", "HINAZU", "SUNNY", "PAON",
        "DONPHAN", "TWINZ", "KIRINRIKI", "PAINTER", "KOUNYA", "RINRIN",
        "BERURUN", "NYOROTONO", "YADOKING", "ANNON", "REDIBA", "MITSUBOSHI",
        "PUCHICORN", "EIFIE", "BLACKY", "TURBAN", "BETBABY", "TEPPOUO",
        "OKUTANK", "GONGU", "KAPOERER", "PUDIE", "HANEKO", "POPONEKO",
        "WATANEKO", "BARIRINA", "LIP", "ELEBABY", "BOOBY", "KIREIHANA",
        "TSUBOMITTO", "MILTANK", "BOMBSEEKER", "GIFT", "KOTORA", "RAITORA",
        "MADAME", "NOROWARA", "KYONPAN", "YAMIKARASU", "HAPPI", "SCISSORS",
        "PURAKKUSU", "DEVIL", "HELGAA", "WOLFMAN", "WARWOLF", "PORYGON2",
        "NAMEIL", "HAGANEIL", "KINGDRA", "RAI", "EN", "SUI",
        "NYULA", "HOUOU", "TOGEPY", "BULU", "TAIL", "LEAFY",
        "FC", "EGG", "FE", "",
    ];

    public static readonly string[] SpeciesNamesJapanese =
    [
        "", "フシギダネ", "フシギソウ", "フシギバナ", "ヒトカゲ", "リザード",
        "リザードン", "ゼニガメ", "カメール", "カメックス", "キャタピー", "トランセル",
        "バタフリー", "ビードル", "コクーン", "スピアー", "ポッポ", "ピジョン",
        "ピジョット", "コラッタ", "ラッタ", "オニスズメ", "オニドリル", "アーボ",
        "アーボック", "ピカチュウ", "ライチュウ", "サンド", "サンドパン", "ニドラン♀",
        "ニドリーナ", "ニドクイン", "ニドラン♂", "ニドリーノ", "ニドキング", "ピッピ",
        "ピクシー", "ロコン", "キュウコン", "プリン", "プクリン", "ズバット",
        "ゴルバット", "ナゾノクサ", "クサイハナ", "ラフレシア", "パラス", "パラセクト",
        "コンパン", "モルフォン", "ディグダ", "ダグトリオ", "ニャース", "ペルシアン",
        "コダック", "ゴルダック", "マンキー", "オコリザル", "ガーディ", "ウインディ",
        "ニョロモ", "ニョロゾ", "ニョロボン", "ケーシィ", "ユンゲラー", "フーディン",
        "ワンリキー", "ゴーリキー", "カイリキー", "マダツボミ", "ウツドン", "ウツボット",
        "メノクラゲ", "ドククラゲ", "イシツブテ", "ゴローン", "ゴローニャ", "ポニータ",
        "ギャロップ", "ヤドン", "ヤドラン", "コイル", "レアコイル", "カモネギ",
        "ドードー", "ドードリオ", "パウワウ", "ジュゴン", "ベトベター", "ベトベトン",
        "シェルダー", "パルシェン", "ゴース", "ゴースト", "ゲンガー", "イワーク",
        "スリープ", "スリーパー", "クラブ", "キングラー", "ビリリダマ", "マルマイン",
        "タマタマ", "ナッシー", "カラカラ", "ガラガラ", "サワムラー", "エビワラー",
        "ベロリンガ", "ドガース", "マタドガス", "サイホーン", "サイドン", "ラッキー",
        "モンジャラ", "ガルーラ", "タッツー", "シードラ", "トサキント", "アズマオウ",
        "ヒトデマン", "スターミー", "バリヤード", "ストライク", "ルージュラ", "エレブー",
        "ブーバー", "カイロス", "ケンタロス", "コイキング", "ギャラドス", "ラプラス",
        "メタモン", "イーブイ", "シャワーズ", "サンダース", "ブースター", "ポリゴン",
        "オムナイト", "オムスター", "カブト", "カブトプス", "プテラ", "カビゴン",
        "フリーザー", "サンダー", "ファイヤー", "ミニリュウ", "ハクリュー", "カイリュー",
        "ミュウツー", "ミュウ", "ハッパ", "ハナモグラ", "ハナリュウ", "ホノオグマ",
        "ボルベアー", "ダイナベア", "クルス", "アクア", "アクエリア", "ホーホー",
        "ボーボー", "パチメエ", "モココ", "デンリュウ", "ミコン", "モンジャ",
        "ジャランラ", "ハネエイ", "プクー", "シビレフグ", "ピチュー", "ピィ",
        "ププリン", "ミズウオ", "ネイティ", "ネイティオ", "ギョピン", "マリル",
        "マンボー１", "イカリ", "グロテス", "エクシング", "パラ", "コクモ",
        "ツーヘッド", "ヨロイドリ", "アニモン", "ヒナーズ", "サニー", "パオン",
        "ドンファン", "ツインズ", "キリンリキ", "ペインター", "コーニャ", "リンリン",
        "ベルルン", "ニョロトノ", "ヤドキング", "アンノーン", "レディバ", "ミツボシ",
        "プチコーン", "エーフィ", "ブラッキー", "ターバン", "ベトベビー", "テッポウオ",
        "オクタン", "ゴング", "カポエラー", "プディ", "ハネコ", "ポポネコ",
        "ワタネコ", "バリリーナ", "リップ", "エレベビー", "ブビィ", "キレイハナ",
        "ツボミット", "ミルタンク", "ボムシカー", "ギフト", "コトラ", "ライトラ",
        "マダーム", "ノロワラ", "キョンパン", "ヤミカラス", "ハッピー", "シザース",
        "プラックス", "デビル", "ヘルガー", "ウルフマン", "ワーウルフ", "ポリゴン２",
        "ナメール", "ハガネール", "キングドラ", "ライ", "エン", "スイ",
        "ニューラ", "ホウオウ", "トゲピー", "ブルー", "テイル", "リーフィ",
        "", "", "", "",
    ];

    public static ReadOnlySpan<byte> BaseStats =>
    [
        0, 0, 0, 0, 0, 0, 45, 49, 49, 45, 65, 65,
        60, 62, 63, 60, 80, 80, 80, 82, 83, 80, 100, 100,
        39, 52, 43, 65, 55, 50, 58, 64, 58, 80, 75, 65,
        78, 84, 78, 100, 100, 85, 44, 48, 65, 43, 50, 55,
        59, 63, 80, 58, 65, 75, 79, 83, 100, 78, 85, 100,
        45, 30, 35, 45, 20, 20, 50, 20, 55, 30, 25, 25,
        60, 45, 50, 70, 80, 80, 40, 35, 30, 50, 20, 20,
        45, 25, 50, 35, 25, 25, 65, 80, 40, 75, 45, 80,
        40, 45, 40, 56, 35, 35, 63, 60, 55, 71, 50, 50,
        83, 80, 75, 91, 70, 70, 30, 56, 35, 72, 25, 35,
        55, 81, 60, 97, 50, 70, 40, 60, 30, 70, 31, 31,
        65, 90, 65, 100, 61, 61, 35, 60, 44, 55, 50, 40,
        60, 85, 69, 80, 85, 65, 35, 55, 30, 90, 50, 40,
        60, 90, 55, 100, 90, 80, 50, 75, 85, 40, 30, 40,
        75, 100, 110, 65, 55, 75, 55, 47, 52, 41, 40, 40,
        70, 62, 67, 56, 45, 55, 90, 82, 87, 76, 55, 75,
        46, 57, 40, 50, 40, 40, 61, 72, 57, 65, 45, 55,
        81, 92, 77, 85, 55, 75, 70, 45, 48, 35, 60, 65,
        95, 70, 73, 60, 85, 95, 38, 41, 40, 65, 65, 45,
        73, 76, 75, 100, 100, 80, 115, 45, 20, 20, 25, 40,
        140, 70, 45, 45, 50, 80, 40, 45, 35, 55, 30, 40,
        75, 80, 70, 90, 55, 75, 45, 50, 55, 30, 75, 70,
        60, 65, 70, 40, 85, 80, 75, 80, 85, 50, 100, 95,
        35, 70, 55, 25, 45, 55, 60, 95, 80, 30, 60, 80,
        60, 55, 50, 45, 40, 50, 70, 65, 60, 90, 90, 100,
        10, 55, 25, 95, 50, 45, 35, 80, 50, 120, 60, 70,
        40, 45, 35, 90, 40, 50, 65, 70, 60, 115, 65, 85,
        50, 52, 48, 55, 50, 50, 80, 82, 78, 85, 80, 80,
        40, 80, 35, 70, 35, 45, 65, 105, 60, 95, 60, 80,
        55, 70, 45, 60, 50, 70, 90, 110, 80, 95, 80, 100,
        40, 50, 40, 90, 40, 45, 65, 65, 65, 90, 50, 65,
        90, 85, 95, 70, 70, 95, 25, 20, 15, 90, 105, 65,
        40, 35, 30, 105, 120, 75, 55, 50, 45, 120, 135, 95,
        70, 80, 50, 35, 35, 40, 80, 100, 70, 45, 50, 60,
        90, 130, 80, 55, 65, 80, 50, 75, 35, 40, 55, 70,
        65, 90, 50, 55, 55, 85, 80, 105, 65, 70, 65, 100,
        40, 40, 35, 70, 60, 100, 80, 70, 65, 100, 80, 120,
        40, 80, 100, 20, 30, 45, 55, 95, 115, 35, 45, 65,
        80, 110, 130, 45, 55, 80, 50, 85, 55, 90, 65, 55,
        65, 100, 70, 105, 80, 75, 90, 65, 65, 15, 40, 35,
        95, 75, 110, 30, 80, 65, 25, 35, 70, 45, 60, 95,
        50, 60, 95, 70, 75, 120, 52, 65, 55, 60, 58, 58,
        35, 85, 45, 75, 35, 35, 60, 110, 70, 100, 60, 65,
        65, 45, 55, 45, 55, 70, 90, 70, 80, 70, 80, 95,
        80, 80, 50, 25, 55, 40, 105, 105, 75, 50, 80, 65,
        30, 65, 100, 40, 45, 30, 50, 95, 180, 70, 85, 70,
        30, 35, 30, 80, 100, 25, 45, 50, 45, 95, 115, 40,
        60, 65, 60, 110, 130, 75, 35, 45, 160, 70, 30, 60,
        60, 48, 45, 42, 45, 90, 85, 73, 70, 67, 70, 115,
        30, 105, 90, 50, 35, 25, 55, 130, 115, 75, 65, 50,
        40, 30, 50, 100, 55, 55, 60, 50, 70, 140, 80, 80,
        60, 40, 80, 40, 60, 55, 95, 95, 85, 55, 125, 75,
        50, 50, 95, 35, 40, 40, 60, 80, 110, 45, 50, 70,
        50, 120, 53, 87, 35, 85, 50, 105, 79, 76, 35, 85,
        90, 55, 75, 30, 60, 90, 40, 65, 95, 35, 60, 40,
        65, 90, 120, 60, 85, 55, 80, 85, 95, 25, 30, 55,
        105, 130, 120, 40, 45, 70, 250, 5, 5, 50, 35, 105,
        65, 55, 115, 60, 55, 100, 105, 95, 80, 90, 40, 80,
        30, 40, 70, 60, 70, 45, 55, 65, 95, 85, 95, 70,
        45, 67, 60, 63, 50, 50, 80, 92, 65, 68, 65, 80,
        30, 45, 55, 85, 70, 55, 60, 75, 85, 115, 100, 85,
        40, 45, 65, 90, 100, 120, 70, 110, 80, 105, 55, 85,
        65, 50, 35, 95, 95, 80, 65, 83, 57, 105, 85, 75,
        65, 95, 57, 93, 85, 70, 65, 125, 100, 85, 55, 75,
        75, 100, 95, 110, 55, 70, 20, 10, 55, 80, 15, 20,
        95, 125, 79, 81, 85, 100, 130, 85, 80, 60, 95, 105,
        48, 48, 48, 48, 48, 48, 55, 55, 50, 55, 45, 65,
        130, 65, 60, 65, 70, 110, 65, 65, 60, 130, 70, 110,
        65, 130, 60, 65, 70, 110, 65, 60, 70, 40, 90, 75,
        35, 40, 100, 35, 90, 55, 70, 60, 125, 55, 115, 70,
        30, 80, 90, 55, 55, 45, 60, 115, 105, 80, 65, 70,
        80, 105, 65, 130, 60, 85, 160, 110, 65, 30, 65, 100,
        90, 85, 100, 85, 95, 125, 90, 90, 85, 100, 125, 90,
        90, 100, 90, 90, 125, 85, 41, 64, 45, 50, 50, 50,
        61, 84, 65, 70, 70, 70, 91, 134, 95, 80, 100, 100,
        106, 110, 90, 130, 154, 90, 100, 100, 100, 100, 100, 100,
        55, 40, 45, 40, 75, 50, 50, 45, 50, 50, 45, 50,
        70, 65, 60, 60, 55, 50, 50, 60, 40, 40, 50, 50,
        60, 70, 50, 50, 60, 50, 70, 80, 60, 60, 70, 50,
        45, 50, 50, 45, 50, 50, 55, 55, 60, 55, 55, 50,
        75, 60, 70, 65, 60, 50, 65, 55, 40, 65, 55, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 55, 45, 45, 50, 70, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        55, 80, 50, 45, 60, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 45, 50, 55, 75, 50,
        45, 50, 50, 60, 50, 50, 55, 50, 50, 80, 70, 50,
        50, 50, 50, 50, 50, 50, 45, 50, 55, 40, 55, 50,
        50, 50, 50, 30, 50, 50, 90, 110, 50, 110, 55, 50,
        60, 65, 60, 30, 80, 50, 60, 65, 50, 85, 45, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        70, 70, 70, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        70, 50, 50, 45, 45, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 40, 65, 40, 70, 65, 50,
        50, 50, 50, 50, 50, 50, 90, 85, 95, 70, 70, 50,
        95, 75, 110, 30, 80, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        40, 50, 35, 50, 50, 50, 50, 50, 45, 50, 60, 50,
        60, 50, 55, 50, 70, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 30, 55, 45, 65, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 110, 50, 60, 40, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        55, 50, 45, 50, 50, 50, 65, 60, 55, 60, 60, 50,
        50, 50, 50, 50, 50, 50, 55, 40, 50, 45, 75, 50,
        60, 50, 55, 55, 70, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
        50, 50, 50, 50, 50, 50, 90, 90, 85, 100, 125, 98,
        90, 100, 90, 90, 125, 99, 90, 85, 100, 85, 125, 97,
        45, 65, 50, 85, 40, 50, 100, 100, 100, 100, 100, 50,
        50, 50, 50, 50, 50, 50, 65, 70, 60, 50, 70, 50,
        55, 55, 50, 60, 60, 50, 50, 50, 50, 50, 50, 50,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    public static ReadOnlySpan<byte> SpeciesTypes =>
    [
        0, 0, 22, 3, 22, 3, 22, 3, 20, 20, 20, 20, 20, 2, 21, 21,
        21, 21, 21, 21, 7, 7, 7, 7, 7, 2, 7, 3, 7, 3, 7, 3,
        0, 2, 0, 2, 0, 2, 0, 0, 0, 0, 0, 2, 0, 2, 3, 3,
        3, 3, 23, 23, 23, 23, 4, 4, 4, 4, 3, 3, 3, 3, 3, 4,
        3, 3, 3, 3, 3, 4, 0, 0, 0, 0, 20, 20, 20, 20, 0, 0,
        0, 0, 3, 2, 3, 2, 22, 3, 22, 3, 22, 3, 7, 22, 7, 22,
        7, 3, 7, 3, 4, 4, 4, 4, 0, 0, 0, 0, 21, 21, 21, 21,
        1, 1, 1, 1, 20, 20, 20, 20, 21, 21, 21, 21, 21, 1, 24, 24,
        24, 24, 24, 24, 1, 1, 1, 1, 1, 1, 22, 3, 22, 3, 22, 3,
        21, 3, 21, 3, 5, 4, 5, 4, 5, 4, 20, 20, 20, 20, 21, 24,
        21, 24, 23, 23, 23, 23, 0, 2, 0, 2, 0, 2, 21, 21, 21, 25,
        3, 3, 3, 3, 21, 21, 21, 25, 8, 3, 8, 3, 8, 3, 5, 4,
        24, 24, 24, 24, 21, 21, 21, 21, 23, 23, 23, 23, 22, 24, 22, 24,
        4, 4, 4, 4, 1, 1, 1, 1, 0, 0, 3, 3, 3, 3, 4, 5,
        4, 5, 0, 0, 22, 22, 0, 0, 21, 21, 21, 21, 21, 21, 21, 21,
        21, 21, 21, 24, 24, 24, 7, 2, 25, 24, 23, 23, 20, 20, 7, 7,
        0, 0, 21, 21, 21, 2, 21, 25, 0, 0, 0, 0, 21, 21, 23, 23,
        20, 20, 0, 0, 5, 21, 5, 21, 5, 21, 5, 21, 5, 2, 0, 0,
        25, 2, 23, 2, 20, 2, 26, 26, 26, 26, 26, 2, 24, 24, 24, 24,
        22, 22, 22, 22, 22, 22, 20, 20, 20, 20, 20, 20, 21, 21, 21, 21,
        21, 21, 2, 2, 2, 2, 23, 23, 23, 23, 23, 23, 21, 21, 22, 22,
        22, 22, 21, 2, 21, 21, 21, 21, 23, 23, 0, 0, 0, 0, 21, 21,
        2, 24, 2, 24, 21, 21, 21, 21, 21, 21, 21, 9, 21, 9, 3, 2,
        7, 22, 7, 3, 7, 3, 2, 9, 0, 0, 0, 2, 22, 24, 4, 4,
        4, 4, 27, 0, 27, 0, 0, 0, 0, 0, 27, 27, 27, 27, 21, 21,
        21, 24, 0, 0, 7, 2, 7, 2, 0, 0, 24, 24, 3, 3, 21, 21,
        3, 3, 21, 21, 21, 21, 1, 1, 1, 1, 20, 20, 22, 2, 22, 2,
        22, 2, 0, 0, 25, 25, 23, 23, 20, 20, 22, 3, 22, 3, 0, 0,
        21, 20, 21, 25, 23, 23, 23, 23, 0, 2, 8, 8, 8, 8, 27, 2,
        0, 0, 7, 2, 7, 7, 20, 20, 20, 20, 25, 25, 25, 25, 0, 0,
        0, 0, 9, 4, 26, 21, 23, 23, 20, 20, 21, 21, 27, 27, 2, 2,
        0, 0, 24, 24, 0, 0, 22, 22, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    public static readonly string[] MoveNames =
    [
        "", "POUND", "KARATE_CHOP", "DOUBLESLAP", "COMET_PUNCH", "MEGA_PUNCH",
        "PAY_DAY", "FIRE_PUNCH", "ICE_PUNCH", "THUNDERPUNCH", "SCRATCH", "VICEGRIP",
        "GUILLOTINE", "RAZOR_WIND", "SWORDS_DANCE", "CUT", "GUST", "WING_ATTACK",
        "WHIRLWIND", "FLY", "BIND", "SLAM", "VINE_WHIP", "STOMP",
        "DOUBLE_KICK", "MEGA_KICK", "JUMP_KICK", "ROLLING_KICK", "SAND_ATTACK", "HEADBUTT",
        "HORN_ATTACK", "FURY_ATTACK", "HORN_DRILL", "TACKLE", "BODY_SLAM", "WRAP",
        "TAKE_DOWN", "THRASH", "DOUBLE_EDGE", "TAIL_WHIP", "POISON_STING", "TWINEEDLE",
        "PIN_MISSILE", "LEER", "BITE", "GROWL", "ROAR", "SING",
        "SUPERSONIC", "SONICBOOM", "DISABLE", "ACID", "EMBER", "FLAMETHROWER",
        "MIST", "WATER_GUN", "HYDRO_PUMP", "SURF", "ICE_BEAM", "BLIZZARD",
        "PSYBEAM", "BUBBLEBEAM", "AURORA_BEAM", "HYPER_BEAM", "PECK", "DRILL_PECK",
        "SUBMISSION", "LOW_KICK", "COUNTER", "SEISMIC_TOSS", "STRENGTH", "ABSORB",
        "MEGA_DRAIN", "LEECH_SEED", "GROWTH", "RAZOR_LEAF", "SOLARBEAM", "POISONPOWDER",
        "STUN_SPORE", "SLEEP_POWDER", "PETAL_DANCE", "STRING_SHOT", "DRAGON_RAGE", "FIRE_SPIN",
        "THUNDERSHOCK", "THUNDERBOLT", "THUNDER_WAVE", "THUNDER", "ROCK_THROW", "EARTHQUAKE",
        "FISSURE", "DIG", "TOXIC", "CONFUSION", "PSYCHIC", "HYPNOSIS",
        "MEDITATE", "AGILITY", "QUICK_ATTACK", "RAGE", "TELEPORT", "NIGHT_SHADE",
        "MIMIC", "SCREECH", "DOUBLE_TEAM", "RECOVER", "HARDEN", "MINIMIZE",
        "SMOKESCREEN", "CONFUSE_RAY", "WITHDRAW", "DEFENSE_CURL", "BARRIER", "LIGHT_SCREEN",
        "HAZE", "REFLECT", "FOCUS_ENERGY", "BIDE", "METRONOME", "MIRROR_MOVE",
        "SELFDESTRUCT", "EGG_BOMB", "LICK", "SMOG", "SLUDGE", "BONE_CLUB",
        "FIRE_BLAST", "WATERFALL", "CLAMP", "SWIFT", "SKULL_BASH", "SPIKE_CANNON",
        "CONSTRICT", "AMNESIA", "KINESIS", "SOFTBOILED", "HI_JUMP_KICK", "GLARE",
        "DREAM_EATER", "POISON_GAS", "BARRAGE", "LEECH_LIFE", "LOVELY_KISS", "SKY_ATTACK",
        "TRANSFORM", "BUBBLE", "DIZZY_PUNCH", "SPORE", "FLASH", "PSYWAVE",
        "SPLASH", "ACID_ARMOR", "CRABHAMMER", "EXPLOSION", "FURY_SWIPES", "BONEMERANG",
        "REST", "ROCK_SLIDE", "HYPER_FANG", "SHARPEN", "CONVERSION", "TRI_ATTACK",
        "SUPER_FANG", "SLASH", "SUBSTITUTE", "STRUGGLE", "SKETCH", "TRIPLE_KICK",
        "THIEF", "SPIDER_WEB", "MIND_READER", "NIGHTMARE", "FLAME_WHEEL", "SNORE",
        "NAIL_DOWN", "FLAIL", "CONVERSION2", "COIN_HURL", "COTTON_SPORE", "REVERSAL",
        "SPITE", "POWDER_SNOW", "PROTECT", "MACH_PUNCH", "SCARY_FACE", "FAINT_ATTACK",
        "SWEET_KISS", "BELLY_DRUM", "SLUDGE_BOMB", "MUD_SLAP", "OCTAZOOKA", "SPIKES",
        "ZAP_CANNON", "FORESIGHT", "DESTINY_BOND", "PERISH_SONG", "SYNCHRONIZE", "DETECT",
        "BONE_LOCK", "LOCK_ON", "OUTRAGE", "SANDSTORM", "GIGA_DRAIN", "ENDURE",
        "CHARM", "FALSE_SWIPE", "SWAGGER", "MILK_DRINK", "SPARK", "FURY_CUTTER",
        "STEEL_WING", "STALKER", "ATTRACT", "SLEEP_TALK", "BELL_CHIME", "RETURN",
        "PRESENT", "FRUSTRATION", "SAFEGUARD", "PAIN_SPLIT", "SACRED_FIRE", "MAGNITUDE",
        "DYNAMICPUNCH", "MEGAPHONE", "DRAGONBREATH", "BATON_PASS", "ENCORE", "PURSUIT",
        "RAPID_SPIN", "TEMPT", "IRON_TAIL", "ROCK_HEAD", "VITAL_THROW", "MORNING_SUN",
        "SYNTHESIS", "MOONLIGHT", "HIDDEN_POWER", "CROSS_CUTTER", "TWISTER", "RAIN_DANCE",
        "SUNNY_DAY", "F2", "F3", "F4", "UPROOT", "WIND_RIDE",
        "WATER_SPORT", "STRONG_ARM", "BRIGHT_MOSS", "WHIRLPOOL", "BOUNCE", "",
        "", "", "", "",
    ];

    public static ReadOnlySpan<byte> MovePP =>
    [
        0, 35, 25, 10, 15, 20, 20, 15, 15, 15, 30, 30, 5, 10, 30, 30,
        35, 35, 20, 15, 20, 20, 10, 20, 30, 5, 25, 15, 15, 15, 25, 20,
        5, 35, 15, 20, 20, 20, 15, 30, 35, 20, 20, 30, 25, 40, 20, 15,
        20, 20, 20, 30, 25, 15, 30, 25, 5, 15, 10, 5, 20, 20, 20, 5,
        35, 20, 25, 20, 20, 20, 15, 20, 10, 10, 40, 25, 10, 35, 30, 15,
        20, 40, 10, 15, 30, 15, 20, 10, 15, 10, 5, 10, 10, 25, 10, 20,
        40, 30, 30, 20, 20, 15, 10, 40, 15, 20, 30, 20, 20, 10, 40, 40,
        30, 30, 30, 20, 30, 10, 10, 20, 5, 10, 30, 20, 20, 20, 5, 15,
        10, 20, 15, 15, 35, 20, 15, 10, 20, 30, 15, 40, 20, 15, 10, 5,
        10, 30, 10, 15, 20, 15, 40, 40, 10, 5, 15, 10, 10, 10, 15, 30,
        30, 10, 10, 20, 10, 0, 1, 10, 10, 10, 10, 10, 10, 10, 10, 10,
        15, 10, 10, 10, 5, 10, 10, 15, 40, 10, 10, 10, 10, 10, 10, 10,
        5, 10, 5, 10, 10, 10, 10, 10, 10, 10, 10, 10, 40, 20, 10, 10,
        20, 20, 10, 10, 10, 10, 10, 10, 10, 10, 10, 5, 10, 10, 10, 40,
        10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 0, 0, 0, 0, 0,
    ];

    public static readonly string[] TypeNames =
    [
        "NORMAL", "FIGHTING", "FLYING", "POISON", "GROUND", "ROCK",
        "BIRD", "BUG", "GHOST", "METAL", "0A", "0B",
        "0C", "0D", "0E", "0F", "10", "11",
        "12", "UNKNOWN", "FIRE", "WATER", "GRASS", "ELECTRIC",
        "PSYCHIC", "ICE", "DRAGON", "DARK",
    ];

    public static readonly string[] Charmap =
    [
        "<NULL>", "<イ゛>", "<ヴ>", "<エ゛>", "<オ゛>", "ガ",
        "ギ", "グ", "ゲ", "ゴ", "ザ", "ジ",
        "ズ", "ゼ", "ゾ", "ダ", "ヂ", "ヅ",
        "デ", "ド", "<ナ゛>", "<ニ゛>", "<ヌ゛>", "<ネ゛>",
        "<ノ゛>", "バ", "ビ", "ブ", "ボ", "<マ゛>",
        "<ミ゛>", "<ム゛>", "<ィ゛>", "<あ゛>", "<い゛>", "<う゛>",
        "<え゛>", "<お゛>", "が", "ぎ", "ぐ", "げ",
        "ご", "ざ", "じ", "ず", "ぜ", "ぞ",
        "だ", "ぢ", "づ", "で", "ど", "<な゛>",
        "<に゛>", "<ぬ゛>", "<ね゛>", "<の゛>", "ば", "び",
        "ぶ", "べ", "ぼ", "<ま゛>", "パ", "ピ",
        "プ", "ポ", "ぱ", "ぴ", "ぷ", "ぺ",
        "ぽ", "<MOM>", "<GA>", "<_CONT>", "<SCROLL>", "<も゜>",
        "<NEXT>", "<LINE>", "@", "<PARA>", "<PLAYER>", "<RIVAL>",
        "#", "<CONT>", "<⋯⋯>", "<DONE>", "<PROMPT>", "<TARGET>",
        "<USER>", "<PC>", "<TM>", "<TRAINER>", "<ROCKET>", "<DEXEND>",
        "■", "▲", "☎", "Ｄ", "Ｅ", "Ｆ",
        "Ｇ", "Ｈ", "Ｉ", "Ｖ", "Ｓ", "Ｌ",
        "Ｍ", "：", "ぃ", "ぅ", "「", "」",
        "『", "』", "・", "⋯", "ぁ", "ぇ",
        "ぉ", "┌", "─", "┐", "│", "└",
        "┘", "　", "ア", "イ", "ウ", "エ",
        "オ", "カ", "キ", "ク", "ケ", "コ",
        "サ", "シ", "ス", "セ", "ソ", "タ",
        "チ", "ツ", "テ", "ト", "ナ", "ニ",
        "ヌ", "ネ", "ノ", "ハ", "ヒ", "フ",
        "ホ", "マ", "ミ", "ム", "メ", "モ",
        "ヤ", "ユ", "ヨ", "ラ", "ル", "レ",
        "ロ", "ワ", "ヲ", "ン", "ッ", "ャ",
        "ュ", "ョ", "ィ", "あ", "い", "う",
        "え", "お", "か", "き", "く", "け",
        "こ", "さ", "し", "す", "せ", "そ",
        "た", "ち", "つ", "て", "と", "な",
        "に", "ぬ", "ね", "の", "は", "ひ",
        "ふ", "へ", "ほ", "ま", "み", "む",
        "め", "も", "や", "ゆ", "よ", "ら",
        "り", "る", "れ", "ろ", "わ", "を",
        "ん", "っ", "ゃ", "ゅ", "ょ", "ー",
        "ﾟ", "ﾞ", "？", "！", "。", "ァ",
        "ゥ", "ェ", "▷", "▶", "▼", "♂",
        "円", "×", "．", "／", "ォ", "♀",
        "０", "１", "２", "３", "４", "５",
        "６", "７", "８", "９",
    ];

    public static string GetSpeciesName(int species) => (uint)species < SpeciesNames.Length && SpeciesNames[species].Length != 0
        ? SpeciesNames[species] : $"Species {species}";

    public static string GetMoveName(int move) => move == 0 ? "(none)"
        : (uint)move < MoveNames.Length && MoveNames[move].Length != 0 ? MoveNames[move] : $"Move {move}";

    public static string GetTypeName(int type) => (uint)type < TypeNames.Length && TypeNames[type].Length != 0
        ? TypeNames[type] : $"Type {type:X2}";

    public static int GetMaxPP(int move, int ppUps)
    {
        if ((uint)move >= MovePP.Length)
            return 0;
        int b = MovePP[move];
        return Math.Min(63, b + ((b / 5) * Math.Min(3, ppUps)));
    }

    public static bool IsPhysicalType(int type) => type < 0x0A;

    public static bool IsCleanName(ReadOnlySpan<byte> raw)
    {
        int chars = 0;
        foreach (var b in raw)
        {
            if (b == 0x50)
                break;
            var ch = Charmap[b];
            if (ch.Length == 0 || ch[0] == '<')
                return false;
            chars++;
        }
        return chars != 0;
    }

    public static string DecodeName(ReadOnlySpan<byte> raw)
    {
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var b in raw)
        {
            var ch = Charmap[b];
            if (ch == "@")
                break;
            sb.Append(ch.Length == 0 ? '?' : ch);
        }
        return sb.ToString();
    }

    public static bool TryEncodeName(string text, Span<byte> dest)
    {
        dest.Fill(0x50);
        int i = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (i >= dest.Length - 1)
                break;
            var s = rune.ToString();
            int index = -1;
            for (int c = 0; c < 256; c++)
            {
                if (Charmap[c] == s)
                { index = c; break; }
            }
            if (index < 0)
                return false;
            dest[i++] = (byte)index;
        }
        if (i < dest.Length)
            dest[i] = 0x50;
        return true;
    }
}
