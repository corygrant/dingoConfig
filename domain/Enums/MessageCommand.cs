namespace domain.Enums;

public enum MessageCommand
{
    Null = 0,
    Read = 1,
    Write = 2,
    ReadParamNotFound = 5,

    ReadAll = 10,
    ReadAllRsp = 11,
    ReadAllComplete = 12,
    ReadAllModified = 13,

    WriteAll = 20,
    WriteAllVal = 21,
    WriteAllComplete = 22,
    WriteAllModified = 23,
    WriteAllMissing = 24,
    WriteAllParamNotFound = 25,
    WriteAllOutOfRange = 26,
    WriteAllMissingDone = 27,

    BurnParams = 30,
    Version = 31,
    Sleep = 32,
    Bootloader = 33,
    CheckCrc = 34,
    CheckCrcRsp = 35,
}