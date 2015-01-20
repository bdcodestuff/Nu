// Aml - A Modular Language.
// Copyright (C) Bryan Edds, 2012-2015.

namespace Aml
open Aml.Ast
open Prime
module AmlConstants =

    let [<Literal>] AmlVersion = 0.045f
    let [<Literal>] EmptyStr = ""
    let [<Literal>] SpaceChar = ' '
    let [<Literal>] SpaceStr = " "
    let [<Literal>] LineCommentChar = ';'
    let [<Literal>] LineCommentStr = ";"
    let [<Literal>] DotChar = '.'
    let [<Literal>] DotStr = "."
    let [<Literal>] HashChar = '#'
    let [<Literal>] HashStr = "#"
    let [<Literal>] BackslashChar = '\\'
    let [<Literal>] BackslashStr = "\\"
    let [<Literal>] ColonChar = ':'
    let [<Literal>] ColonStr = ":"
    let [<Literal>] DollarChar = '$'
    let [<Literal>] DollarStr = "$"
    let [<Literal>] PercentChar = '%'
    let [<Literal>] PercentStr = "%"
    let [<Literal>] AmpersandChar = '&'
    let [<Literal>] AmpersandStr = "&"
    let [<Literal>] AtChar = '@'
    let [<Literal>] AtStr = "@"
    let [<Literal>] NameSeparatorChar = '/'
    let [<Literal>] NameSeparatorStr = "/"
    let [<Literal>] DoubleQuoteChar = '\"'
    let [<Literal>] DoubleQuoteStr = "\""
    let [<Literal>] OpenParenChar = '('
    let [<Literal>] OpenParenStr = "("
    let [<Literal>] CloseParenChar = ')'
    let [<Literal>] CloseParenStr = ")"
    let [<Literal>] OpenBracketChar = '['
    let [<Literal>] OpenBracketStr = "["
    let [<Literal>] CloseBracketChar = ']'
    let [<Literal>] CloseBracketStr = "]"
    let [<Literal>] OpenCurlyChar = '{'
    let [<Literal>] OpenCurlyStr = "{"
    let [<Literal>] CloseCurlyChar = '}'
    let [<Literal>] CloseCurlyStr = "}"
    let [<Literal>] OpenTriangleStr = "<|"
    let [<Literal>] CloseTriangleStr = "|>"
    let [<Literal>] TypeIndicatorChar = '-'
    let [<Literal>] TypeIndicatorStr = "-"
    let [<Literal>] NegativePrefixChar = '-'
    let [<Literal>] PositivePrefixChar = '+'
    let [<Literal>] NewlineChars = "\n\r"
    let [<Literal>] VerbatimChars = "\n\r\""
    let [<Literal>] WhitespaceChars = " \t\n\r"
    let [<Literal>] WhitespaceCharsAndParens = " \t\n\r()"
    let [<Literal>] WhitespaceCharsAndBrackets = " \t\n\r[]"
    let [<Literal>] WhitespaceCharsAndCurlies = " \t\n\r{}"
    let [<Literal>] ReservedChars = "&_|?`,"
    let [<Literal>] PrefixChars = "$%&@"
    let [<Literal>] SpecialNameChars = "-+!^*='<>/"
    let [<Literal>] OpenMultilineCommentStr = "#|"
    let [<Literal>] CloseMultilineCommentStr = "|#"
    let [<Literal>] DoubleColonStr = "::"
    let [<Literal>] EllipsisStr = "..."
    let [<Literal>] FloatFloorStr = "fFloor"
    let [<Literal>] FloatCeilingStr = "fCeiling"
    let [<Literal>] FloatTruncateStr = "fTruncate"
    let [<Literal>] FloatRoundStr = "fRound"
    let [<Literal>] FloatExpStr = "fExp"
    let [<Literal>] FloatLogStr = "fLog"
    let [<Literal>] FloatSqrtStr = "fSqrt"
    let [<Literal>] FloatSinStr = "fSin"
    let [<Literal>] FloatCosStr = "fCos"
    let [<Literal>] FloatTanStr = "fTan"
    let [<Literal>] FloatAsinStr = "fAsin"
    let [<Literal>] FloatAcosStr = "fAcos"
    let [<Literal>] FloatAtanStr = "fAtan"
    let [<Literal>] DoubleFloorStr = "dFloor"
    let [<Literal>] DoubleCeilingStr = "dCeiling"
    let [<Literal>] DoubleTruncateStr = "dTruncate"
    let [<Literal>] DoubleRoundStr = "dRound"
    let [<Literal>] DoubleExpStr = "dExp"
    let [<Literal>] DoubleLogStr = "dLog"
    let [<Literal>] DoubleSqrtStr = "dSqrt"
    let [<Literal>] DoubleSinStr = "dSin"
    let [<Literal>] DoubleCosStr = "dCos"
    let [<Literal>] DoubleTanStr = "dTan"
    let [<Literal>] DoubleAsinStr = "dAsin"
    let [<Literal>] DoubleAcosStr = "dAcos"
    let [<Literal>] DoubleAtanStr = "dAtan"
    let [<Literal>] IntPlusStr = "i+"
    let [<Literal>] IntMinusStr = "i-"
    let [<Literal>] IntMultiplyStr = "i*"
    let [<Literal>] IntDivideStr = "i/"
    let [<Literal>] IntPowStr = "iPow"
    let [<Literal>] IntRemStr = "iRem"
    let [<Literal>] IntIncStr = "iInc"
    let [<Literal>] IntDecStr = "iDec"
    let [<Literal>] LongPlusStr = "g+"
    let [<Literal>] LongMinusStr = "g-"
    let [<Literal>] LongMultiplyStr = "g*"
    let [<Literal>] LongDivideStr = "g/"
    let [<Literal>] LongPowStr = "gPow"
    let [<Literal>] LongRemStr = "gRem"
    let [<Literal>] LongIncStr = "gInc"
    let [<Literal>] LongDecStr = "gDec"
    let [<Literal>] FloatPlusStr = "f+"
    let [<Literal>] FloatMinusStr = "f-"
    let [<Literal>] FloatMultiplyStr = "f*"
    let [<Literal>] FloatDivideStr = "f/"
    let [<Literal>] FloatPowStr = "fPow"
    let [<Literal>] FloatRemStr = "fRem"
    let [<Literal>] FloatLogNStr = "fLogN"
    let [<Literal>] FloatRootStr = "fRoot"
    let [<Literal>] DoublePlusStr = "d+"
    let [<Literal>] DoubleMinusStr = "d-"
    let [<Literal>] DoubleMultiplyStr = "d*"
    let [<Literal>] DoubleDivideStr = "d/"
    let [<Literal>] DoublePowStr = "dPow"
    let [<Literal>] DoubleRemStr = "dRem"
    let [<Literal>] DoubleLogNStr = "dLogN"
    let [<Literal>] DoubleRootStr = "dRoot"
    let [<Literal>] CharEqualStr = "c="
    let [<Literal>] CharInequalStr = "c/="
    let [<Literal>] CharLessThanStr = "c<"
    let [<Literal>] CharGreaterThanStr = "c>"
    let [<Literal>] CharLessThanOrEqualStr = "c<="
    let [<Literal>] CharGreaterThanOrEqualStr = "c>="
    let [<Literal>] IntEqualStr = "i="
    let [<Literal>] IntInequalStr = "i/="
    let [<Literal>] IntLessThanStr = "i<"
    let [<Literal>] IntGreaterThanStr = "i>"
    let [<Literal>] IntLessThanOrEqualStr = "i<="
    let [<Literal>] IntGreaterThanOrEqualStr = "i>="
    let [<Literal>] LongEqualStr = "g="
    let [<Literal>] LongInequalStr = "g/="
    let [<Literal>] LongLessThanStr = "g<"
    let [<Literal>] LongGreaterThanStr = "g>"
    let [<Literal>] LongLessThanOrEqualStr = "g<="
    let [<Literal>] LongGreaterThanOrEqualStr = "g>="
    let [<Literal>] FloatEqualStr = "f="
    let [<Literal>] FloatInequalStr = "f/="
    let [<Literal>] FloatLessThanStr = "f<"
    let [<Literal>] FloatGreaterThanStr = "f>"
    let [<Literal>] FloatLessThanOrEqualStr = "f<="
    let [<Literal>] FloatGreaterThanOrEqualStr = "f>="
    let [<Literal>] DoubleEqualStr = "d="
    let [<Literal>] DoubleInequalStr = "d/="
    let [<Literal>] DoubleLessThanStr = "d<"
    let [<Literal>] DoubleGreaterThanStr = "d>"
    let [<Literal>] DoubleLessThanOrEqualStr = "d<="
    let [<Literal>] DoubleGreaterThanOrEqualStr = "d>="
    let [<Literal>] AndStr = "and"
    let [<Literal>] OrStr = "or"
    let [<Literal>] XorStr = "xor"
    let [<Literal>] NandStr = "nand"
    let [<Literal>] NorStr = "nor"
    let [<Literal>] DocStr = "doc"
    let [<Literal>] IfStr = "if"
    let [<Literal>] AttemptStr = "attempt"
    let [<Literal>] LetStr = "let"
    let [<Literal>] ApplyStr = "apply"
    let [<Literal>] TypeStr = "type"
    let [<Literal>] TypeOfStr = "typeOf"
    let [<Literal>] EquatableStr = "equatable"
    let [<Literal>] EqualStr = "equal"
    let [<Literal>] InequalStr = "inequal"
    let [<Literal>] EqualityStr = "="
    let [<Literal>] InequalityStr = "/="
    let [<Literal>] RefEqualityStr = "=="
    let [<Literal>] RefInequalityStr = "/=="
    let [<Literal>] ConsStr = "cons"
    let [<Literal>] HeadStr = "head"
    let [<Literal>] TailStr = "tail"
    let [<Literal>] StringLengthStr = "sLength"
    let [<Literal>] StringAppendStr = "s+"
    let [<Literal>] ListLengthStr = "tLength"
    let [<Literal>] ListAppendStr = "t+"
    let [<Literal>] ArrayLengthStr = "aLength"
    let [<Literal>] ArrayAppendStr = "a+"
    let [<Literal>] ExtendStr = "extend"
    let [<Literal>] CaseStr = "case"
    let [<Literal>] ConditionStr = "condition"
    let [<Literal>] HideStr = "hide"
    let [<Literal>] InterveneStr = "intervene"
    let [<Literal>] RefStr = "ref"
    let [<Literal>] GetStr = "get"
    let [<Literal>] SetStr = "set!"
    let [<Literal>] StepsStr = "steps!"
    let [<Literal>] WhileStr = "while!"
    let [<Literal>] ViolationStr = "violation"
    let [<Literal>] BooleanStr = "bool"
    let [<Literal>] CharacterStr = "char"
    let [<Literal>] StringStr = "string"
    let [<Literal>] IntStr = "int"
    let [<Literal>] LongStr = "long"
    let [<Literal>] FloatStr = "float"
    let [<Literal>] DoubleStr = "double"
    let [<Literal>] KeywordStr = "keyword"
    let [<Literal>] PackageStr = "package"
    let [<Literal>] IntSuffixStr = "i"
    let [<Literal>] LongSuffixStr = "g"
    let [<Literal>] FloatSuffixStr = "f"
    let [<Literal>] DoubleSuffixStr = "d"
    let [<Literal>] RequirementStr = "req"
    let [<Literal>] PreconditionStr = "pre"
    let [<Literal>] PostconditionStr = "post"
    let [<Literal>] WhereStr = "where"
    let [<Literal>] DefinitionStr = "def"
    let [<Literal>] StructureStr = "struct"
    let [<Literal>] SignatureStr = "sig"
    let [<Literal>] ProtocolStr = "protocol"
    let [<Literal>] InstanceStr = "instance"
    let [<Literal>] AffirmationStr = "affirmation"
    let [<Literal>] UsingFileStr = "usingFile"
    let [<Literal>] UsingLanguageStr = "usingLanguage"
    let [<Literal>] IsUnitStr = "isUnit"
    let [<Literal>] IsBooleanStr = "isBool"
    let [<Literal>] IsCharacterStr = "isChar"
    let [<Literal>] IsStringStr = "isString"
    let [<Literal>] IsIntStr = "isInt"
    let [<Literal>] IsLongStr = "isLong"
    let [<Literal>] IsFloatStr = "isFloat"
    let [<Literal>] IsDoubleStr = "isDouble"
    let [<Literal>] IsKeywordStr = "isKeyword"
    let [<Literal>] IsPackageStr = "isPackage"
    let [<Literal>] IsLambdaStr = "isLambda"
    let [<Literal>] IsListStr = "isList"
    let [<Literal>] IsArrayStr = "isArray"
    let [<Literal>] IsCompositeStr = "isComposite"
    let [<Literal>] HasTypeStr = "hasType"
    let [<Literal>] HasProtocolStr = "hasProtocol"
    let [<Literal>] CharToIntStr = "charToInt"
    let [<Literal>] IntToCharStr = "intToChar"
    let [<Literal>] IntToLongStr = "intToLong"
    let [<Literal>] LongToIntStr = "longToInt"
    let [<Literal>] FloatToDoubleStr = "floatToDouble"
    let [<Literal>] DoubleToFloatStr = "doubleToFloat"
    let [<Literal>] IntToFloatStr = "intToFloat"
    let [<Literal>] FloatToIntStr = "floatToInt"
    let [<Literal>] LongToDoubleStr = "longToDouble"
    let [<Literal>] DoubleToLongStr = "doubleToLong"
    let [<Literal>] StringToArrayStr = "stringToArray"
    let [<Literal>] ArrayToStringStr = "arrayToString"
    let [<Literal>] ListToArrayStr = "listToArray"
    let [<Literal>] ArrayToListStr = "arrayToList"
    let [<Literal>] SpecialValueStr = "specialValue"
    let [<Literal>] FunStr = "fun"
    let [<Literal>] UnitStr = "unit"
    let [<Literal>] ListStr = "list"
    let [<Literal>] ArrayStr = "array"
    let [<Literal>] CompositeStr = "composite"
    let [<Literal>] SelectorStr = "select"
    let [<Literal>] ArgStr = "arg"
    let [<Literal>] ResultStr = "result"
    let [<Literal>] ProblemStr = "problem"
    let [<Literal>] DataStr = "data"
    let [<Literal>] MissingStr = "missing"
    let [<Literal>] ReloadStr = "reload"
    let [<Literal>] TrueStr = "#t"
    let [<Literal>] FalseStr = "#f"
    let [<Literal>] NameStr = "name"
    let [<Literal>] DispatchStr = "dispatch"
    let [<Literal>] IsStr = "is"
    let [<Literal>] AStr = "a"
    let [<Literal>] XStr = "x"
    let [<Literal>] YStr = "y"
    let [<Literal>] ViolationPrefixStr = ":v/"
    let [<Literal>] SimpleEntryPrefixStr = ":e/"
    let [<Literal>] MemberPrefixStr = ":m/"
    let [<Literal>] ProtocolPrefixStr = ":p/"
    let [<Literal>] InstancePrefixStr = ":i/"
    let [<Literal>] TypePrefixStr = ":t/"
    let [<Literal>] LanguagePrefixStr = ":l/"
    let EquatableProtocolStr = ProtocolPrefixStr + EquatableStr
    let ViolationTypeStr = TypePrefixStr + ViolationStr
    let BooleanTypeStr = TypePrefixStr + BooleanStr
    let CharacterTypeStr = TypePrefixStr + CharacterStr
    let StringTypeStr = TypePrefixStr + StringStr
    let IntTypeStr = TypePrefixStr + IntStr
    let LongTypeStr = TypePrefixStr + LongStr
    let FloatTypeStr = TypePrefixStr + FloatStr
    let DoubleTypeStr = TypePrefixStr + DoubleStr
    let KeywordTypeStr = TypePrefixStr + KeywordStr
    let PackageTypeStr = TypePrefixStr + PackageStr
    let SpecialValueTypeStr = TypePrefixStr + SpecialValueStr
    let LambdaTypeStr = TypePrefixStr + FunStr
    let UnitTypeStr = TypePrefixStr + UnitStr
    let RefTypeStr = TypePrefixStr + RefStr
    let ListTypeStr = TypePrefixStr + ListStr
    let ArrayTypeStr = TypePrefixStr + ArrayStr
    let CompositeTypeStr = TypePrefixStr + CompositeStr
    let MemberNameStr = MemberPrefixStr + NameStr