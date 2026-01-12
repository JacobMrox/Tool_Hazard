namespace IntelOrca.Biohazard.Room
{
    public static class RdtFileChunkKinds
    {
        public const int Unknown = 0;

        public const int RDT1Header = 0x0100 | 0;
        public const int RDT1LIT = 0x0100 | 1;
        public const int RDT1Offsets = 0x0100 | 2;
        public const int RDT1RVD = 0x0100 | 3;
        public const int RDT1SCA = 0x0100 | 4;
        public const int RDT1EmbeddedModelTable = 0x0100 | 5;
        public const int RDT1EmbeddedItemTable = 0x0100 | 6;
        public const int RDT1BLK = 0x0100 | 7;
        public const int RDT1FLR = 0x0100 | 8;
        public const int RDT1InitSCD = 0x0100 | 9;
        public const int RDT1MainSCD = 0x0100 | 10;
        public const int RDT1EventSCD = 0x0100 | 11;
        public const int RDT1EMR = 0x0100 | 12;
        public const int RDT1EDD = 0x0100 | 13;
        public const int RDT1MSG = 0x0100 | 14;
        public const int RDT1EmbeddedItemIcons = 0x0100 | 15;
        public const int RDT1ESPIDs = 0x0100 | 16;
        public const int RDT1ESPEFFTable = 0x0100 | 17;
        public const int RDT1ESPTIMTable = 0x0100 | 18;
        public const int RDT1EDT = 0x0100 | 19;
        public const int RDT1VH = 0x0100 | 20;
        public const int RDT1VB = 0x0100 | 21;
        public const int RDT1RID = 0x0100 | 22;
        public const int RDT1EmbeddedObjectTmd = 0x0100 | 23;
        public const int RDT1EmbeddedObjectTim = 0x0100 | 24;
        public const int RDT1EmbeddedItemTmd = 0x0100 | 25;
        public const int RDT1EmbeddedItemTim = 0x0100 | 26;
        public const int RDT1EmbeddedEspTim = 0x0100 | 27;
        public const int RDT1EmbeddedCamTim = 0x0100 | 28;
        public const int RDT1EmbeddedCamMask = 0x0100 | 29;
        public const int RDT1EmbeddedEspEff = 0x0100 | 30;

        public const int Header = 0x0200 | 0;
        public const int OffsetTable = 0x0200 | 1;

        public const int RDT2EDT = 0x0200 | 2;
        public const int RDT2VH = 0x0200 | 3;
        public const int RDT2VB = 0x0200 | 4;
        public const int EmbeddedTrialVH = 0x0200 | 5;
        public const int EmbeddedTrialVB = 0x0200 | 6;
        public const int RDT2OVA = 0x0200 | 7;
        public const int RDT2SCA = 0x0200 | 8;
        public const int RDT2RID = 0x0200 | 9;
        public const int RDT2RVD = 0x0200 | 10;
        public const int RDT2LIT = 0x0200 | 11;
        public const int RDT2EmbeddedObjectTable = 0x0200 | 12;
        public const int RDT2FLR = 0x0200 | 13;
        public const int RDT2BLK = 0x0200 | 14;
        public const int RDT2MSGJA = 0x0200 | 15;
        public const int RDT2MSGEN = 0x0200 | 16;
        public const int RDT2TIMSCROLL = 0x0200 | 17;
        public const int RDT2SCDINIT = 0x0200 | 18;
        public const int RDT2SCDMAIN = 0x0200 | 19;
        public const int RDT2ESPID = 0x0200 | 20;
        public const int RDT2EspEffTable = 0x0200 | 21;
        public const int RDT2TIMESP = 0x0200 | 22;
        public const int ObjectTextures = 0x0200 | 23;
        public const int RDT2RBJ = 0x0200 | 24;

        public const int MD2TIMOBJECT = 0x0200 | 25;
        public const int RDT2MD1OBJECT = 0x0200 | 26;
        public const int RDT2EmbeddedCamMask = 0x0200 | 27;
        public const int RDT2ESPTIMTABLE = 0x0100 | 28;
        public const int RDT3ETD = 0x0200 | 30;

        public const int RDTCVHeader = 0x0300 | 0x00;
        public const int RDTCVTableOffsets = 0x0300 | 0x01;
        public const int RDTCVTableCounts = 0x0300 | 0x02;
        public const int RDTCVUDAH = 0x0300 | 0x03;
        public const int RDTCVCamera = 0x0300 | 0x04;
        public const int RDTCVLighting = 0x0300 | 0x05;
        public const int RDTCVEnemy = 0x0300 | 0x06;
        public const int RDTCVObject = 0x0300 | 0x07;
        public const int RDTCVItem = 0x0300 | 0x08;
        public const int RDTCVEffect = 0x0300 | 0x09;
        public const int RDTCVBoundary = 0x0300 | 0x0A;
        public const int RDTCVAot = 0x0300 | 0x0B;
        public const int RDTCVTrigger = 0x0300 | 0x0C;
        public const int RDTCVPlayer = 0x0300 | 0x0D;
        public const int RDTCVEvent = 0x0300 | 0x0E;
        public const int RDTCVUnknown1 = 0x0300 | 0x0F;
        public const int RDTCVUnknown2 = 0x0300 | 0x10;
        public const int RDTCVAction = 0x0300 | 0x11;
        public const int RDTCVText = 0x0300 | 0x12;
        public const int RDTCVSysmes = 0x0300 | 0x13;
        public const int RDTCVModel = 0x0300 | 0x14;
        public const int RDTCVMotion = 0x0300 | 0x15;
        public const int RDTCVScript = 0x0300 | 0x16;
        public const int RDTCVTexture = 0x0300 | 0x17;
    }
}
