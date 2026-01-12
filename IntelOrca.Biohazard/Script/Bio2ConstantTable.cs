using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IntelOrca.Biohazard.Script
{
    public class Bio2ConstantTable : IConstantTable
    {
        private static readonly SortedDictionary<string, int> _constantMap = new SortedDictionary<string, int>();

        private const byte SCE_MESSAGE = 4;
        private const byte SCE_EVENT = 5;
        private const byte SCE_FLAG_CHG = 6;

        public string GetEnemyName(byte kind) => g_enemyNames.Namify("ENEMY_", kind);
        public string GetItemName(byte kind) => g_itemNames.Namify("ITEM_", kind);

        private string GetKeyName(byte value)
        {
            return value switch
            {
                0 => "UNLOCKED",
                254 => "UNLOCK",
                255 => "LOCKED",
                _ => GetItemName(value)
            };
        }

        private string GetProcedureName(int value)
        {
            return value switch
            {
                0 => "main",
                1 => "aot",
                _ => $"main_{value:X2}",
            };
        }

        private string? GetItemFlagsName(int value)
        {
            if (value == 0)
                return "IF_DEFAULT";

            if ((value & 0b0000_1110) != 0)
                return null;

            var glint = value & 0b1110_0000;
            var sz = glint switch
            {
                0xA0 => "IF_GLINT_GRAY",
                0xC0 => "IF_GLINT_BLUE",
                0xE0 => "IF_GLINT_RED",
                _ => ""
            };
            if ((value & 0b0001_0000) != 0)
                sz += $" | IF_FAST";
            if ((value & 0b0000_0001) != 0)
                sz += $" | IF_FLOOR";
            if (sz.StartsWith(" | "))
                sz = sz.Substring(3);
            if (sz == "IF_FAST" && value != 0x10)
                return null;
            if (sz == "IF_FLOOR" && value != 1)
                return null;
            if (sz == "")
                return null;
            return sz;
        }

        public byte? FindOpcode(string name, bool isEventOpcode)
        {
            for (int i = 0; i < g_instructionSignatures.Length; i++)
            {
                var signature = g_instructionSignatures[i];
                if (signature == "")
                    continue;

                var opcodeName = signature;
                var colonIndex = signature.IndexOf(':');
                if (colonIndex != -1)
                    opcodeName = signature.Substring(0, colonIndex);

                if (name == opcodeName)
                    return (byte)i;
            }
            return null;
        }

        public string? GetConstant(byte opcode, int pIndex, BinaryReader reader)
        {
            using (var br = reader.Fork())
            {
                if (opcode == (byte)OpcodeV2.Ck)
                {
                    if (pIndex == 1)
                    {
                        var F = br.ReadByte();
                        var f = br.ReadByte();
                        return GetFlagName(F, f);
                    }
                }
                else if (opcode == (byte)OpcodeV2.WorkSet)
                {
                    if (pIndex == 1)
                    {
                        var kind = br.ReadByte();
                        var id = br.ReadByte();
                        if (kind == 3)
                            return GetConstant('2', id);
                        else if (kind == 4)
                            return GetConstant('1', id);
                    }
                }
                else if (opcode == (byte)OpcodeV2.AotReset)
                {
                    if (pIndex == 3)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_MESSAGE)
                        {
                            br.BaseStream.Position += 1;
                            return GetConstant('3', br.ReadByte());
                        }
                    }
                    if (pIndex == 5)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 3;
                            return GetConstant('g', br.ReadByte());
                        }
                        else if (sce == SCE_FLAG_CHG)
                        {
                            br.BaseStream.Position += 3;
                            return GetConstant('t', br.ReadByte());
                        }
                    }
                    else if (pIndex == 6)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 3;
                            if (br.ReadByte() == (byte)OpcodeV2.Gosub)
                            {
                                return GetConstant('p', br.ReadByte());
                            }
                        }
                    }
                    else if (pIndex == 7)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_FLAG_CHG)
                        {
                            br.BaseStream.Position += 5;
                            return GetConstant('p', br.ReadByte());
                        }
                    }
                }
                else if (opcode == (byte)OpcodeV2.AotSet)
                {
                    if (pIndex == 9)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_MESSAGE)
                        {
                            br.BaseStream.Position += 11;
                            return GetConstant('3', br.ReadByte());
                        }
                    }
                    if (pIndex == 11)

                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 13;
                            return GetConstant('g', br.ReadByte());
                        }
                        else if (sce == SCE_FLAG_CHG)
                        {
                            br.BaseStream.Position += 13;
                            return GetConstant('t', br.ReadByte());
                        }
                    }
                    else if (pIndex == 12)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT || sce == SCE_FLAG_CHG)
                        {
                            br.BaseStream.Position += 13;
                            if (br.ReadByte() == (byte)OpcodeV2.Gosub)
                            {
                                return GetConstant('p', br.ReadByte());
                            }
                        }
                    }
                    else if (pIndex == 13)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_FLAG_CHG)
                        {
                            br.BaseStream.Position += 15;
                            return GetConstant('p', br.ReadByte());
                        }
                    }
                }
                else if (opcode == (byte)OpcodeV2.AotSet4p)
                {
                    if (pIndex == 15)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 21;
                            return GetConstant('g', br.ReadByte());
                        }
                    }
                    else if (pIndex == 16)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 21;
                            if (br.ReadByte() == (byte)OpcodeV2.Gosub)
                            {
                                return GetConstant('p', br.ReadByte());
                            }
                        }
                    }
                }
                return null;
            }
        }

        public string? GetConstant(char kind, int value)
        {
            return kind switch
            {
                '0' => $"ID_AOT_{value}",
                '1' => $"ID_OBJ_{value}",
                '2' => $"ID_EM_{value}",
                '3' => $"ID_MSG_{value}",
                '4' => $"SBK_{value}",
                '5' => $"DOR_{value}",
                'a' => GetConstantFlags(g_satNames, value, "SAT_AUTO"),
                // 'b'
                'c' => GetConstantName(g_comparators, value),
                'e' => GetEnemyName((byte)value),
                'f' => GetConstantName(g_flagGroups, value),
                'g' => value == (byte)OpcodeV2.Gosub ? "I_GOSUB" : null,
                'h' => GetConstantFlags(g_aiNames, value, "AI_DEFAULT"),
                // 'i'
                'm' => GetConstantName(g_entityMembers, value),
                'n' => GetItemFlagsName(value),
                'o' => GetConstantName(g_operators, value),
                'p' => GetProcedureName(value),
                's' => GetConstantName(g_sceNames, value),
                'T' => GetKeyName((byte)value),
                't' => GetKeyName((byte)value),
                // 'u'
                'v' => GetNamedVariable(value),
                'w' => GetConstantName(g_workKinds, value),
                'x' => GetConstantName(g_bgmChannel, value),
                'y' => GetConstantName(g_bgmOp, value),
                'z' => GetConstantName(g_bgmType, value),
                _ => null
            };
        }

        private string? GetConstantName(string?[] table, int value)
        {
            if (value >= 0 && value < table.Length)
                return table[value];
            return null;
        }

        private string? GetConstantFlags(string?[] table, int value, string? zeroName)
        {
            if (value == 0)
                return zeroName;
            var sb = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                var mask = 1 << i;
                if (value == mask)
                {
                    return table[i];
                }
                else if ((value & (1 << i)) != 0)
                {
                    sb.Append(table[i]);
                    sb.Append(" | ");
                }
            }
            sb.Remove(sb.Length - 3, 3);
            return sb.ToString();
        }

        private int? FindConstantValue(string symbol, char kind)
        {
            for (int i = 0; i < 256; i++)
            {
                var name = GetConstant(kind, i);
                if (name == symbol)
                    return i;
            }
            return null;
        }

        public int? GetConstantValue(string symbol)
        {
            if (_constantMap.Count == 0)
            {
                _constantMap.Add("I_GOSUB", (byte)OpcodeV2.Gosub);
                var constChars = new char[]
                {
                    '0', '1', '2', '3', '4', '5', 'a', 'c', 'e', 'f', 'g', 'h', 'm', 'n', 'o', 's', 't', 'v', 'w', 'x', 'y', 'z'
                };
                foreach (var ch in constChars)
                {
                    for (var i = 0; i < 256; i++)
                    {
                        var name = GetConstant(ch, i);
                        if (name != null && !name.Contains(" ") && !char.IsNumber(name[0]))
                        {
                            if (_constantMap.TryGetValue(name, out var check) && check != i)
                                throw new InvalidOperationException();
                            else
                                _constantMap[name] = i;
                        }
                        for (var j = 0; j < 32; j++)
                        {
                            var s = GetFlagName(j, i);
                            if (s != null)
                            {
                                if (_constantMap.TryGetValue(s, out var check) && check != i)
                                    throw new InvalidOperationException();
                                else
                                    _constantMap[s] = i;
                            }
                        }
                    }
                }
            }
            if (!_constantMap.TryGetValue(symbol, out var value))
                return null;
            return value;
        }

        public int GetInstructionSize(byte opcode, BinaryReader? br, bool isEventOpcode)
        {
            if (opcode < g_instructionSizes.Length)
                return g_instructionSizes[opcode];
            return 0;
        }

        public string GetOpcodeSignature(byte opcode, bool isEventOpcode)
        {
            if (opcode < g_instructionSignatures.Length)
                return g_instructionSignatures[opcode];
            return "";
        }

        public bool IsOpcodeCondition(byte opcode)
        {
            switch ((OpcodeV2)opcode)
            {
                case OpcodeV2.Ck:
                case OpcodeV2.Cmp:
                case OpcodeV2.MemberCmp:
                case OpcodeV2.KeepItemCk:
                case OpcodeV2.WorkCopy:
                case OpcodeV2.SceKeyCk:
                case OpcodeV2.SceTrgCk:
                    return true;
            }
            return false;
        }

        public string? GetNamedFlag(int group, int index)
        {
            var groupName = GetConstant('F', group);
            var flagName = GetFlagName(group, index);
            flagName ??= index.ToString();
            return $"${groupName}.{flagName})";
        }

        private static string? GetFlagName(int group, int index)
        {
            return group switch
            {
                0 when index == 0x19 => "F_DIFFICULT",
                1 when index == 0 => "F_PLAYER",
                1 when index == 1 => "F_SCENARIO",
                1 when index == 5 => "F_EASY",
                1 when index == 6 => "F_BONUS",
                1 when index == 0x1B => "F_CUTSCENE",
                0xB when index == 0x1F => "F_QUESTION",
                _ => null
            };
        }

        public string? GetNamedVariable(int index)
        {
            return index switch
            {
                2 => "V_USED_ITEM",
                16 => "V_TEMP",
                24 => "V_STAGE",
                25 => "V_ROOM",
                26 => "V_CUT",
                27 => "V_LAST_RDT",
                29 => "V_RNG",
                32 => "V_NEXT_PLD",
                _ => $"V_{index:X2}",
            };
        }

        private static string?[] g_flagGroups = new[]
        {
            "FG_SYSTEM",
            "FG_STATUS",
            "FG_STOP",
            "FG_SCENARIO",
            "FG_COMMON",
            "FG_ROOM",
            "FG_ENEMY",
            "FG_ENEMY_2",
            "FG_ITEM",
            "FG_MAP",
            "FG_USE",
            "FG_MESSAGE",
            "FG_ROOM_ENEMY",
            "FG_PBF00",
            "FG_PBF01",
            "FG_PBF02",
            "FG_PBF03",
            "FG_PBF04",
            "FG_PBF05",
            "FG_PBF06",
            "FG_PBF07",
            "FG_PBF08",
            "FG_PBF09",
            "FG_PBF0A",
            "FG_PBF0B",
            "FG_PBF0C",
            "FG_PBF0D",
            "FG_PBF0E",
            "FG_PBF0F",
            "FG_ZAPPING",
            "FG_RBJ_SET",
            "FG_KEY",
            "FG_MAP_C",
            "FG_MAP_I",
            "FG_ITEM_2",
        };

        private static string?[] g_entityMembers = new[]
        {
            "M_POINTER",
            "M_BE_FLAG",
            "M_ROUTINE0",
            "M_ROUTINE1",
            "M_ROUTINE2",
            "M_ROUTINE3",
            "M_ID",
            "M_TYPE",
            "M_OBJ_NO",
            "M_SCE_NO",
            "M_ATTRIBUTE",
            "M_X_POS",
            "M_Y_POS",
            "M_Z_POS",
            "M_X_DIR",
            "M_Y_DIR",
            "M_Z_DIR",
            "M_FLOOR",
            "M_STATUS_FLAG",
            "M_GROUND",
            "M_X_DEST",
            "M_Z_DEST",
            "M_SCE_FLAG",
            "M_SCE_FREE0",
            "M_SCE_FREE1",
            "M_SCE_FREE2",
            "M_SCE_FREE3",
            "M_X_SPEED0",
            "M_X_SPEED1",
            "M_Y_SPEED",
            "M_Z_SPEED",
            "M_HOKAN_FLAG",
            "M_OBJ_OFS_X",
            "M_OBJ_OFS_Y",
            "M_OBJ_OFS_Z",
            "M_OBJ_W",
            "M_OBJ_H",
            "M_OBJ_D",
            "M_PARTS_POS_Y",
            "M_SCA_OLD_X",
            "M_SCA_OLD_Z",
            "M_FREE0",
            "M_FREE1",
            "M_DAMAGE_CNT",
        };

        private static int[] g_instructionSizes = new int[]
        {
            1, 2, 1, 4, 4, 2, 4, 4, 1, 4, 3, 1, 1, 6, 2, 4,
            2, 4, 2, 4, 6, 2, 2, 6, 2, 2, 2, 6, 1, 4, 1, 1,
            1, 4, 4, 6, 4, 3, 6, 4, 1, 2, 1, 6, 20, 38, 3, 4,
            1, 1, 8, 8, 4, 3, 12, 4, 3, 8, 16, 32, 2, 3, 6, 4,
            8, 10, 1, 4, 22, 5, 10, 2, 16, 8, 2, 3, 5, 22, 22, 4,
            4, 6, 6, 6, 22, 6, 4, 8, 4, 4, 2, 2, 3, 2, 2, 2,
            14, 4, 2, 1, 16, 2, 1, 28, 40, 30, 6, 4, 1, 4, 6, 2,
            1, 1, 16, 8, 4, 22, 3, 4, 6, 1, 16, 16, 6, 6, 6, 6,
            2, 3, 3, 1, 2, 6, 1, 1, 3, 1, 6, 6, 8, 24, 24
        };

        private string[] g_enemyNames = new string[]
        {
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "Zombie (Cop)",
            "Zombie (Brad)",
            "Zombie (Guy1)",
            "Zombie (Girl)",
            "",
            "Zombie (TestSubject)",
            "Zombie (Scientist)",
            "Zombie (Naked)",
            "Zombie (Guy2)",
            "",
            "",
            "",
            "",
            "",
            "Zombie (Guy3)",
            "Zombie (Random)",
            "Zombie Dog",
            "Crow",
            "Licker (Red)",
            "Alligator",
            "Licker (Grey)",
            "Spider",
            "Baby Spider",
            "G Embryo",
            "G Adult",
            "Cockroach",
            "Tyrant 1",
            "Tyrant 2",
            "",
            "Zombie Arms",
            "Ivy",
            "Vines",
            "Birkin 1",
            "Birkin 2",
            "Birkin 3",
            "Birkin 4",
            "Birkin 5",
            "",
            "",
            "",
            "",
            "Ivy (Purple)",
            "Giant Moth",
            "Maggots",
            "",
            "",
            "",
            "",
            "Chief Irons 1",
            "Ada Wong 1",
            "Chief Irons 2",
            "Ada Wong 2",
            "Ben Bertolucci 1",
            "Sherry (Pendant)",
            "BenBertolucci 2",
            "AnnetteBirkin 1",
            "Robert Kendo",
            "Annette Birkin 2",
            "Marvin Branagh",
            "Mayors Daughter",
            "",
            "",
            "",
            "Sherry (Jacket)",
            "Leon Kennedy (Rpd)",
            "Claire Redfield",
            "",
            "",
            "Leon Kennedy (Bandaged)",
            "Claire Redfield (No Jacket)",
            "",
            "",
            "Leon Kennedy (Tank Top)",
            "Claire Redfield (Cow Girl)",
            "Leon Kennedy (Leather)",
        };

        private string[] g_itemNames = new string[]
        {
            "None",
            "Knife",
            "HandgunLeon",
            "HandgunClaire",
            "CustomHandgun",
            "Magnum",
            "CustomMagnum",
            "Shotgun",
            "CustomShotgun",
            "GrenadeLauncherExplosive",
            "GrenadeLauncherFlame",
            "GrenadeLauncherAcid",
            "Bowgun",
            "ColtSAA",
            "Sparkshot",
            "SMG",
            "Flamethrower",
            "RocketLauncher",
            "GatlingGun",
            "Beretta",
            "HandgunAmmo",
            "ShotgunAmmo",
            "MagnumAmmo",
            "FuelTank",
            "ExplosiveRounds",
            "FlameRounds",
            "AcidRounds",
            "SMGAmmo",
            "SparkshotAmmo",
            "BowgunAmmo",
            "InkRibbon",
            "SmallKey",
            "HandgunParts",
            "MagnumParts",
            "ShotgunParts",
            "FAidSpray",
            "AntivirusBomb",
            "ChemicalACw32",
            "HerbG",
            "HerbR",
            "HerbB",
            "HerbGG",
            "HerbGR",
            "HerbGB",
            "HerbGGG",
            "HerbGGB",
            "HerbGRB",
            "Lighter",
            "Lockpick",
            "PhotoSherry",
            "ValveHandle",
            "RedJewel",
            "RedCard",
            "BlueCard",
            "SerpentStone",
            "JaguarStone",
            "JaguarStoneL",
            "JaguarStoneR",
            "EagleStone",
            "BishopPlug",
            "RookPlug",
            "KnightPlug",
            "KingPlug",
            "WeaponBoxKey",
            "Detonator",
            "C4Explosive",
            "C4Detonator",
            "Crank",
            "FilmA",
            "FilmB",
            "FilmC",
            "UnicornMedal",
            "EagleMedal",
            "WolfMedal",
            "Cog",
            "ManholeOpener",
            "MainFuse",
            "FuseCase",
            "Vaccine",
            "VaccineCart",
            "FilmD",
            "VaccineBase",
            "GVirus",
            "SpecialKey",
            "JointPlugBlue",
            "JointPlugRed",
            "Cord",
            "PhotoAda",
            "CabinKey",
            "SpadeKey",
            "DiamondKey",
            "HeartKey",
            "ClubKey",
            "DownKey",
            "UpKey",
            "PowerRoomKey",
            "MODisk",
            "UmbrellaKeyCard",
            "MasterKey",
            "PlatformKey",
            "",
            "",
            "",
            "",
            "ChrisDiary",
            "FederalPoliceReport",
            "MemotoLeon",
            "PoliceMemorandum",
            "OperationReport1",
            "MailToTheChief1",
            "MailToTheChief2",
            "SecretaryDiaryA",
            "SecretaryDiaryB",
            "OperationReport2",
            "UserRegistration",
            "DevelopedFilmA",
            "DevelopedFilmB",
            "DevelopedFilmC",
            "PatrolReport",
            "WatchmanDiary",
            "ChiefDiary",
            "SewerManagerDiary",
            "SewerManagerFax",
            "DevelopedFilmD",
            "VaccineSynthesis",
            "LabSecurityManual",
            "PEpsilonReport",
            "HintFiles1",
            "HintFiles2",
        };

        private static string[] g_instructionSignatures = new string[]
        {
            "nop",
            "evt_end",
            "evt_next",
            "evt_chain:ugp",
            "evt_exec:ugp",
            "evt_kill",
            "if:uL",
            "else:u@",
            "endif",
            "sleep:uU",
            "sleeping:U",
            "wsleep",
            "wsleeping",
            "for:uLU",
            "next",
            "while:uL",

            "ewhile",
            "do:uL",
            "edwhile:'",
            "switch:uL",
            "case:uLU",
            "default",
            "eswitch",
            "goto:uuu~",
            "gosub:p",
            "return",
            "break",
            "for2",
            "break_point",
            "work_copy",
            "nop_1E",
            "nop_1F",

            "nop_20",
            "ck:fuu",
            "set:fuu",
            "cmp:uvcI",
            "save:vI",
            "copy:vv",
            "calc:uovI",
            "calc2:ovv",
            "sce_rnd",
            "cut_chg",
            "cut_old",
            "message_on:u3uuu",
            "aot_set:0sauuIIIIuuuuuu",
            "obj_model_set:1uuuuUUIIIIIIIIIIIIuuuu",
            "work_set:wu",
            "speed_set:uI",

            "add_speed",
            "add_aspeed",
            "pos_set:uIII",
            "dir_set:uIII",
            "member_set:mI",
            "member_set2:mv",
            "se_on:uIIIII",
            "sca_id_set",
            "flr_set",
            "dir_ck",
            "sce_espr_on:uUUUIIII",
            "door_aot_se:0sauuIIIIIIIIuuuuuuuutu",
            "cut_auto",
            "member_copy:vm",
            "member_cmp",
            "plc_motion",

            "plc_dest:uuuII",
            "plc_neck:uIIIuu",
            "plc_ret",
            "plc_flg:uU",
            "sce_em_set:u2euhu4uuIIIIUU",
            "col_chg_set",
            "aot_reset:0sauuuuuu",
            "aot_on:0",
            "super_set:uuuIIIIII",
            "super_reset:uIII",
            "plc_gun",
            "cut_replace",
            "sce_espr_kill",
            "",
            "item_aot_set:0sauuIIUUTUU1n",
            "sce_key_ck:uU",

            "sce_trg_ck:uU",
            "sce_bgm_control:xyzuu",
            "sce_espr_control",
            "sce_fade_set",
            "sce_espr3d_on:uUUUIIIIIII",
            "member_calc:oUI",
            "member_calc2:ouu",
            "sce_bgmtbl_set:uuuUU",
            "plc_rot:uU",
            "xa_on:uU",
            "weapon_chg",
            "plc_cnt",
            "sce_shake_on",
            "mizu_div_set",
            "keep_item_ck:t",
            "xa_vol",

            "kage_set:uuuuuIIII",
            "cut_be_set",
            "sce_item_lost:t",
            "plc_gun_eff",
            "sce_espr_on2",
            "sce_espr_kill2",
            "plc_stop",
            "aot_set_4p:usauuIIIIIIIIuuuuuu",
            "door_aot_set_4p:0sauuIIIIIIIIIIIIuuu5uuuutu",
            "item_aot_set_4p:0sauuIIIIIIIITUUuu",
            "light_pos_set:uuuI",
            "light_kido_set:uI",
            "rbj_reset",
            "sce_scr_move:uI",
            "parts_set:uuuI",
            "movie_on",

            "splc_ret",
            "splc_sce",
            "super_on",
            "mirror_set",
            "sce_fade_adjust",
            "sce_espr3d_on2",
            "sce_item_get",
            "sce_line_start",
            "sce_line_main",
            "sce_line_end",
            "sce_parts_bomb",
            "sce_parts_down",
            "light_color_set",
            "light_pos_set2:uuuI",
            "light_kido_set2:uuuU",
            "light_color_set2",

            "se_vol",
            "",
            "",
            "heal",
            "",
            "",
            "poison_ck",
            "poison_clr",
            "sce_item_ck_lost:tu",
            "heal_partner",
            "nop_8a",
            "nop_8b",
            "nop_8c",
            "",
            "",
        };

        private static readonly string[] g_comparators = new string[]
        {
            "CMP_EQ",
            "CMP_GT",
            "CMP_GE",
            "CMP_LT",
            "CMP_LE",
            "CMP_NE"
        };

        private static readonly string[] g_operators = new string[]
        {
            "OP_ADD",
            "OP_SUB",
            "OP_MUL",
            "OP_DIV",
            "OP_MOD",
            "OP_OR",
            "OP_AND",
            "OP_XOR",
            "OP_NOT",
            "OP_LSL",
            "OP_LSR",
            "OP_ASR"
        };

        private static readonly string[] g_satNames = new string[]
        {
            "SAT_PL",
            "SAT_EM",
            "SAT_SPL",
            "SAT_OB",
            "SAT_MANUAL",
            "SAT_FRONT",
            "SAT_UNDER",
            "0x80"
        };

        private static readonly string[] g_sceNames = new string[] {
            "SCE_AUTO",
            "SCE_DOOR",
            "SCE_ITEM",
            "SCE_NORMAL",
            "SCE_MESSAGE",
            "SCE_EVENT",
            "SCE_FLAG_CHG",
            "SCE_WATER",
            "SCE_MOVE",
            "SCE_SAVE",
            "SCE_ITEMBOX",
            "SCE_DAMAGE",
            "SCE_STATUS",
            "SCE_HIKIDASHI",
            "SCE_WINDOWS"
        };

        private static readonly string[] g_workKinds = new string[]
        {
            "WK_NONE",
            "WK_PLAYER",
            "WK_SPLAYER",
            "WK_ENEMY",
            "WK_OBJECT",
            "WK_DOOR",
            "WK_ALL"
        };

        private static readonly string[] g_aiNames = new string[]
        {
            "AI_01",
            "AI_02",
            "AI_04",
            "AI_08",
            "AI_10",
            "AI_20",
            "AI_40",
            "AI_INACTIVE"
        };

        private static readonly string[] g_bgmChannel = new string[]
        {
            "BGM_CHANNEL_MAIN",
            "BGM_CHANNEL_SUB0",
            "BGM_CHANNEL_SUB1",
        };

        private static readonly string[] g_bgmOp = new string[]
        {
            "BGM_OP_NOP",
            "BGM_OP_START",
            "BGM_OP_STOP",
            "BGM_OP_RESTART",
            "BGM_OP_PAUSE",
            "BGM_OP_FADEOUT",
        };

        private static readonly string[] g_bgmType = new string[]
        {
            "BGM_TYPE_MAIN_VOL",
            "BGM_TYPE_PROG0_VOL",
            "BGM_TYPE_PROG1_VOL",
            "BGM_TYPE_PROG2_VOL",
        };
    }
}
