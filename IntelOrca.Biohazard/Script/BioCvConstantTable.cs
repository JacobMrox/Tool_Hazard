using System;
using System.IO;

namespace IntelOrca.Biohazard.Script
{
    internal class BioCvConstantTable : IConstantTable
    {
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

        public string? GetConstant(char kind, int value)
        {
            return kind switch
            {
                '0' => $"ID_ITEM_{value}",
                '1' => $"ID_AOT_{value}",
                'f' => GetFlagName(value),
                'p' => GetProcedureName(value),
                'T' => GetItemName((byte)value),
                't' => GetItemName((byte)value),
                _ => null
            };
        }

        public string? GetConstant(byte opcode, int pIndex, BinaryReader reader)
        {
            using var br = reader.Fork();
            if (opcode == 0x04 || opcode == 0x05)
            {
                if (pIndex == 2)
                {
                    var a = br.ReadByte();
                    var b = br.ReadUInt16();
                    var c = br.ReadByte();
                    if (a == 10 && b == 23)
                        return GetConstant('1', c);
                }
            }
            else if (opcode == 0x06)
            {
                if (pIndex == 2)
                {
                    var var = br.ReadByte();
                    br.ReadByte();
                    var value = br.ReadByte();
                    if (var == 8)
                        return GetConstant('t', value);
                }
            }
            else if (opcode == 0x08)
            {
                if (pIndex == 1)
                {
                    var var = br.ReadByte();
                    var value = br.ReadByte();
                    if (var == 8)
                        return GetConstant('t', value);
                }
            }
            return null;
        }

        public int? GetConstantValue(string symbol)
        {
            return null;
        }

        private string GetFlagName(int value)
        {
            return value switch
            {
                1 => "FG_COMMON",
                3 => "FG_ENEMY",
                4 => "FG_LOCAL",
                7 => "FG_ITEM",
                10 => "FG_AOT",
                _ => $"FG_{value}"
            };
        }

        private string GetProcedureName(int value)
        {
            return $"main_{value + 2:X2}";
        }

        public string GetEnemyName(byte kind)
        {
            throw new NotImplementedException();
        }

        public string GetItemName(byte kind) => g_itemNames.Namify("ITEM_", kind);

        public string? GetNamedFlag(int obj, int index)
        {
            throw new NotImplementedException();
        }

        public string? GetNamedVariable(int index)
        {
            throw new NotImplementedException();
        }

        public int GetInstructionSize(byte opcode, BinaryReader? br, bool isEventOpcode = false)
        {
            if (opcode == 0x64 || opcode == 0x66 || opcode == 0x67 || opcode == 0x69)
            {
                if (br == null)
                    return 1;

                if (br.BaseStream.Position == br.BaseStream.Length)
                    return 1;

                var byte2 = br.ReadByte();
                br.BaseStream.Position--;
                var opcode2 = (ushort)((opcode << 8) | byte2);
                return opcode2 switch
                {
                    0x6407 => 8,
                    0x640D => 6,
                    0x6434 => 2,
                    0x6480 => 2,
                    0x6481 => 12,
                    0x6482 => 2,
                    0x6483 => 4,
                    0x6489 => 4,
                    0x648B => 2,
                    0x648E => 2,
                    0x6496 => 4,
                    0x6601 => 2,
                    0x6608 => 2,
                    0x660C => 6,
                    0x6614 => 4,
                    0x6780 => 2,
                    0x678F => 6,
                    0x6793 => 8,
                    0x6794 => 4,
                    0x6900 => 5,
                    0x6902 => 3,
                    0x6903 => 3,
                    0x6904 => 3,
                    0x6905 => 6,
                    0x6906 => 6,
                    0x6907 => 8,
                    0x6908 => 8,
                    0x6909 => 6,
                    0x690A => 6,
                    0x690B => 6,
                    0x690C => 6,
                    0x690D => 6,
                    0x690F => 6,
                    0x6910 => 6,
                    0x6911 => 4,
                    0x6912 => 4,
                    0x6913 => 4,
                    0x6917 => 4,
                    0x6918 => 8,
                    0x6919 => 8,
                    0x691A => 10,
                    0x691B => 8,
                    0x691C => 6,
                    0x691D => 3,
                    0x691E => 3,
                    0x691F => 3,
                    0x6920 => 3,
                    0x6921 => 3,
                    0x6922 => 10,
                    0x6924 => 2,
                    0x6926 => 10,
                    0x6928 => 4,
                    0x6929 => 4,
                    0x692A => 4,
                    0x692C => 5,
                    0x692E => 4,
                    0x692F => 2,
                    0x6930 => 4,
                    0x6931 => 4,
                    0x6932 => 4,
                    0x6933 => 6,
                    0x6934 => 4,
                    0x6935 => 4,
                    _ => 1,
                };
            }
            return _opcodeSizes[opcode];
        }

        public string GetOpcodeSignature(byte opcode, bool isEventOpcode = false)
        {
            return g_instructionSignatures[opcode];
        }

        public bool IsOpcodeCondition(byte opcode)
        {
            return opcode switch
            {
                0x04 => true,
                0x06 => true,
                0x07 => true,
                0x10 => true,
                0x11 => true,
                0x21 => true,
                0x26 => true,
                0x5D => true,
                0x72 => true,
                0x81 => true,
                0x8A => true,
                _ => false,
            };
        }

        private byte[] _opcodeSizes = new byte[]
        {
            2, 2, 2, 2, 6, 6, 4, 6, 4, 4, 4, 4, 4, 4, 6, 2, 2,
            2, 2, 4, 4, 4, 2, 8, 2, 8, 2, 6, 6, 4,
            6, 4, 4, 6, 4, 6, 4, 10, 2, 2, 2, 2, 4,
            2, 4, 2, 2, 4, 4, 2, 12, 8, 12, 4, 6, 2,
            6, 4, 2, 6, 4, 4, 4, 4, 8, 2, 4, 4,
            6, 2, 8, 10, 6, 10, 8, 4, 6, 4, 14, 6, 2, 12,
            12, 12, 12, 2, 6, 2, 14, 12, 4, 2, 2, 6, 4, 2,
            6, 2, 6, 2, 0, 4, 0, 0, 6, 0, 2, 2, 8, 22, 22, 12,
            2, 2, 14, 22, 4, 4, 4, 6, 14, 6, 2, 10, 10,
            4, 4, 6, 4, 2, 4, 2, 8, 4, 16, 2, 10, 2,
            2, 12, 6, 2, 4, 4, 2, 4, 2, 2, 4, 4, 2, 8, 12,
            4, 4, 6, 2, 4, 2, 2, 6, 2, 4, 12, 4, 4,
            4, 4, 10, 2, 10, 6, 6, 4, 2, 4, 22, 6, 4, 8, 4,
            4, 2, 2, 2, 10, 16, 2, 2, 2, 6, 4, 28, 2, 6, 4, 8,
            2, 2, 4, 8, 4, 2, 2, 4, 2, 2, 2, 4, 10, 10,
            2, 4, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 4, 3, 4, 2, 2, 1, 1, 2,
        };

        private static string[] g_instructionSignatures = new string[]
        {
            "end",
            "if:\'",
            "else:l",
            "endif:u",
            "ck:fUuu",
            "set:fUuu",
            "cmpb:uuu",
            "cmpw:uuuU",
            "setb:uuu",
            "setw:uU",
            "set_wall_atari:uuu",
            "set_etc_atari:1uu",
            "set_floor_atari:uuu",
            "ck_death:uU",
            "ck_item:0U1u",
            "clr_use_item:u",
            "ck_use_item:t",
            "ck_player_item:t",
            "set_cinematic:u",
            "set_camera:uuu",
            "event_on:uup",
            "bgm_on:uuu",
            "bgm_off:u",
            "se_on:uuuUuu",
            "se_off:u",
            "voice_on:uUuuuu",
            "voice_off:u",
            "ck_adx:uuuuu",
            "bg_se_on:uUuu",
            "bg_se_off:uuu",
            "ck_adx_time:uUuu",
            "set_message:uuu",
            "set_display_object:uuu",
            "ck_death_event:uUuu",
            "set_ck_enemy:uU",
            "set_ck_item:0U1u",
            "set_init_model",
            "set_etc_atari2:1Uuuuuuu",
            "ck_arms_item",
            "change_arms_item",
            "sub_status",
            "set_camera_pause",
            "set_camera_2",
            "set_motion_pause",
            "set_effect:uU",
            "init_motion_pause",
            "set_player_motion_pause",
            "init_set_kage",
            "init_motion_pause_ex",
            "player_item_lost:t",
            "set_object_link:uuuuuUUU",
            "set_door_call:uUuuuu",
            "set_player_object_link:uuuuuUUU",
            "set_light",
            "set_fade",
            "room_case_no",
            "ck_frame:uuuU",
            "set_camera_info",
            "set_player_muteki",
            "set_default_model",
            "set_mask",
            "set_lip",
            "start_mask",
            "start_lip",
            "set_player_start_look_g:uUUU",
            "set_player_stop_look_g",
            "set_item_aspd:uuu",
            "set_effect_display:uuu",
            "set_effect_amb",
            "delete_object_se",
            "set_next_room_bgm:uuuUuu",
            "set_next_room_bg_se:uuuUUuu",
            "call_foot_se",
            "call_weapon_se",
            "set_yakkyou",
            "set_light_type",
            "set_fog_color",
            "ck_player_item_block",
            "set_effect_blood:uuuUUUuuuu",
            "set_cyouten_henkei",
            "set_object_motion",
            "set_object_enemy_link:uuuuuUUU",
            "set_object_item_link:uuuuuUUU",
            "set_enemy_item_link:uuuuuUUU",
            "set_enemy_enemy_link:uuuuuUUU",
            "start_cyouten_henkei",
            "set_effect_blood_pool:uuuU",
            "fix_event_camera_player",
            "set_effect_blood_pool_2:uUUUuuuuU",
            "set_object_object_link:uuuuuUUU",
            "set_camera_yure:uU",
            "set_init_camera",
            "set_message_display_end",
            "check_pad",
            "start_movie",
            "stop_movie",
            "check_t_frame:uuuU",
            "check_event_timer",
            "check_camera",
            "set_random",
            "player_ctr",
            "load_work",
            "object_ctr",
            "sub_ctr",
            "load_work_2",
            "common_ctr",
            "event_skip_set",
            "delete_yakkyou",
            "object_alpha_set",
            "cyodan_set",
            "h_effect_set",
            "object_link_set_object_ply:uuuuuUUU",
            "effect_push",
            "effect_pop",
            "area_search_object",
            "light_parameter_c_set",
            "light_parameter_start",
            "init_midi_slot_set",
            "d_sound_flag_set",
            "sound_volume_set",
            "light_parameter_set",
            "enemy_se_on",
            "enemy_se_off",
            "wal_atari_set_2:uUuuuuuu",
            "flr_atari_set2:uUuuuuuu",
            "motion_pos_set_enemy_ply",
            "kage_sw_set",
            "sound_pan_set",
            "init_pony_set",
            "sub_map_busy_check",
            "set_debug_loop_ex",
            "sound_fade_out",
            "cyouten_henkei_set_ex",
            "cyouten_henkei_start_ex",
            "easy_s_e_set",
            "sound_flag_re_set",
            "effect_uv_set",
            "player_change_set",
            "player_poison_check",
            "add_object_se",
            "rand_test",
            "event_com_set",
            "zombie_up_death_check",
            "face_pause_set",
            "face_re_set",
            "effect_mode_set",
            "bg_se_off_2",
            "bgm_off_2",
            "bg_se_on_2",
            "bgm_on_2",
            "effect_sensya_set",
            "effect_kokuen_set",
            "effect_sand_set",
            "enemy_hp_up",
            "face_rep",
            "movie_check",
            "set_item_motion",
            "object_aspd_set",
            "puru_puru_flag_set",
            "puru_puru_start",
            "map_system_on",
            "set_trap_damage",
            "event_lighter_fire_set",
            "object_link_set_ply_item",
            "player_kaidan_motion",
            "enemy_render_set",
            "bgm_on_ex",
            "bgm_on_2_ex",
            "fog_parameter_c_set",
            "fog_parameter_start",
            "effect_uv_set_2",
            "bg_color_set",
            "movie_time_check",
            "effect_type_set",
            "player_poison_2_cr",
            "ply_hand_change",
            "h_effect_set_2",
            "object_dpos_check",
            "item_get_get",
            "etc_atari_enemy_pos_set",
            "etc_atari_event_pos_set",
            "load_work_ex",
            "room_sound_case",
            "item_player_to_box",
            "item_box_to_player:t",
            "grd_pos_set",
            "grd_pos_move_c_set",
            "grd_pos_move_start",
            "event_kill:p",
            "re_try_point_set",
            "ply_dpos_check",
            "player_item_lost_ex",
            "cyodan_set_ex",
            "arms_item_set",
            "item_get_get_ex:uuuU",
            "effect_sand_set_matsumoto",
            "voice_wait",
            "voice_start",
            "game_over_set",
            "player_item_change_m:ttu",
            "effect_baku_drm_set",
            "player_item_tama:tU",
            "effect_clear_event",
            "event_timer_set",
            "enemy_look_flag_set",
            "return_title_event",
            "syukan_mode_set",
            "ex_game_item_init",
            "set_enemy_life_m",
            "set_effect_ssize",
            "set_effect_link_offset",
            "ranking_call",
            "call_sys_se",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "nothing",
            "e_while_2",
            "com_next",
            "nothing",
            "nothing",
            "nothing",
            "sleep:uU",
            "sleeping:uu",
            "for:uU",
            "next:u",
            "while:u",
            "e_while",
            "event_next",
            "event_end:u",
        };

        private static string[] g_itemNames = new[]
        {
            "None",
            "RocketLauncher",
            "AssaultRifle",
            "SniperRifle",
            "Shotgun",
            "HandgunGlock17",
            "GrenadeLauncher",
            "BowGun",
            "CombatKnife",
            "Handgun",
            "CustomHandgun",
            "LinearLauncher",
            "HandgunBullets",
            "MagnumBullets",
            "ShotgunShells",
            "GrenadeRounds",
            "AcidRounds",
            "FlameRounds",
            "BowGunArrows",
            "M93RPart",
            "FAidSpray",
            "GreenHerb",
            "RedHerb",
            "BlueHerb",
            "MixedHerb2Green",
            "MixedHerbRedGreen",
            "MixedHerbBlueGreen",
            "MixedHerb2GreenBlue",
            "MixedHerb3Green",
            "MixedHerbGreenBlueRed",
            "MagnumBulletsInsideCase",
            "InkRibbon",
            "Magnum",
            "GoldLugers",
            "SubMachineGun",
            "BowGunPowder",
            "GunPowderArrow",
            "BOWGasRounds",
            "MGunBullets",
            "GasMask",
            "RifleBullets",
            "DuraluminCaseUnused",
            "ARifleBullets",
            "AlexandersPierce",
            "AlexandersJewel",
            "AlfredsRing",
            "AlfredsJewel",
            "PrisonersDiary",
            "DirectorsMemo",
            "Instructions",
            "Lockpick",
            "GlassEye",
            "PianoRoll",
            "SteeringWheel",
            "CraneKey",
            "Lighter",
            "EaglePlate",
            "SidePack",
            "MapRoll",
            "HawkEmblem",
            "QueenAntObject",
            "KingAntObject",
            "BiohazardCard",
            "DuraluminCaseM93RParts",
            "Detonator",
            "ControlLever",
            "GoldDragonfly",
            "SilverKey",
            "GoldKey",
            "ArmyProof",
            "NavyProof",
            "AirForceProof",
            "KeyWithTag",
            "IDCard",
            "Map",
            "AirportKey",
            "EmblemCard",
            "SkeletonPicture",
            "MusicBoxPlate",
            "GoldDragonflyNoWings",
            "Album",
            "Halberd",
            "Extinguisher",
            "Briefcase",
            "PadlockKey",
            "TG01",
            "SpAlloyEmblem",
            "ValveHandle",
            "OctaValveHandle",
            "MachineRoomKey",
            "MiningRoomKey",
            "BarCodeSticker",
            "SterileRoomKey",
            "DoorKnob",
            "BatteryPack",
            "HemostaticWire",
            "TurnTableKey",
            "ChemStorageKey",
            "ClementAlpha",
            "ClementSigma",
            "TankObject",
            "SpAlloyEmblemUnused",
            "AlfredsMemo",
            "RustedSword",
            "Hemostatic",
            "SecurityCard",
            "SecurityFile",
            "AlexiasChoker",
            "AlexiasJewel",
            "QueenAntRelief",
            "KingAntRelief",
            "RedJewel",
            "BlueJewel",
            "Socket",
            "SqValveHandle",
            "Serum",
            "EarthenwareVase",
            "PaperWeight",
            "SilverDragonflyNoWings",
            "SilverDragonfly",
            "WingObject",
            "Crystal",
            "GoldDragonfly1Wing",
            "GoldDragonfly2Wings",
            "GoldDragonfly3Wings",
            "File",
            "PlantPot",
            "PictureB",
            "DuraluminCaseBowGunPowder",
            "DuraluminCaseMagnumRounds",
            "BowGunPowderUnused",
            "EnhancedHandgun",
            "Memo",
            "BoardClip",
            "Card",
            "NewspaperClip",
            "LugerReplica",
            "QueenAntReliefComplete",
            "FamilyPicture",
            "FileFolders",
            "RemoteController",
            "QuestionA",
            "M1P",
            "CalicoBullets",
            "ClementMixture",
            "PlayingManual",
            "QuestionB",
            "QuestionC",
            "QuestionD",
            "EmptyExtinguisher",
            "SquareSocket",
            "QuestionE",
            "CrestKeyS",
            "CrestKeyG"
        };
    }
}
