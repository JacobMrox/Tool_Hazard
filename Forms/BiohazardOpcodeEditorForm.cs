using IntelOrca.Biohazard;
using System.Data;
using Tool_Hazard.Biohazard.SCD;
using Tool_Hazard.Biohazard.SCD.Opcodes;

namespace Tool_Hazard.Forms
{
    public partial class BiohazardOpcodeEditorForm : Form
    {
        private readonly BioVersion _version;
        private string? _currentScdPath;
        private List<ScdInstruction> _instructions = new();
        private List<ScdInstructionRow> _rows = new();
        private int SelectedInstructionIndex
        {
            get
            {
                if (gridOpcodes.CurrentRow?.DataBoundItem is not ScdInstructionRow row)
                    return -1;

                return _rows.IndexOf(row);
            }
        }
        private bool _isRefreshing;
        private enum NumberViewMode
        {
            Hex,
            Decimal
        }

        private NumberViewMode _numberViewMode = NumberViewMode.Hex;

        public BiohazardOpcodeEditorForm(BioVersion version)
        {
            InitializeComponent();

            //Update view checkboxes
            hexidecimalToolStripMenuItem.Checked = _numberViewMode == NumberViewMode.Hex;
            decimalToolStripMenuItem.Checked = _numberViewMode == NumberViewMode.Decimal;

            //Set version
            _version = version;

            //Update version menu checks and load opcode database for the selected version
            UpdateVersionMenuChecks();   // ← highlight correct one immediately
            OpcodeDatabase.LoadForVersion(_version);
            SetupGrid();
        }
        private void SetStatus(string text)
        {
            Text = $"Biohazard OpCode Editor - {text}";
            lblStatus.Text = $"Biohazard OpCode Editor - {text}";
        }

        //Helper methods
        private void GridOpcodes_SelectionChanged(object? sender, EventArgs e)
        {
            if (gridOpcodes.CurrentRow?.DataBoundItem is not ScdInstructionRow row)
                return;

            // show the raw instruction object in property grid
            //propDetails.SelectedObject = row.Tag;
        }
        private void UpdateVersionMenuChecks()
        {
            bIO1RE1ToolStripMenuItem.Checked = _version == BioVersion.Biohazard1;
            bIO15RE15ToolStripMenuItem.Checked = _version == BioVersion.Biohazard1_5;
            bIO2RE2ToolStripMenuItem.Checked = _version == BioVersion.Biohazard2;
            bIO3RE3ToolStripMenuItem.Checked = _version == BioVersion.Biohazard3;
        }

        // When user edits the Params cell and finishes editing, try to parse the text and apply it to the instruction object
        private void GridOpcodes_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (gridOpcodes.Columns[e.ColumnIndex].DataPropertyName != "Params")
                return;

            if (gridOpcodes.Rows[e.RowIndex].DataBoundItem is not ScdInstructionRow row)
                return;

            var inst = (ScdInstruction)row.Tag!;
            var text = row.Params ?? "";

            try
            {
                ApplyParamsTextToInstruction(inst, text);

                // regenerate normalized display text (so formatting is consistent)
                row.Params = FormatParams(inst);
                gridOpcodes.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Invalid params:\n{ex.Message}", "Parse error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // revert to last known-good
                row.Params = FormatParams(inst);
                gridOpcodes.Refresh();
            }
        }

        private static void ApplyParamsTextToInstruction(ScdInstruction inst, string text)
        {
            // Expected: "Aot=0 SCE=1 SAT=33 X=-28032 ..."
            var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var eq = part.IndexOf('=');
                if (eq <= 0 || eq == part.Length - 1)
                    throw new FormatException($"Bad token '{part}' (expected Key=Value)");

                var key = part.Substring(0, eq).Trim();
                var valStr = part.Substring(eq + 1).Trim();

                // Only allow keys that exist in the opcode schema
                if (inst.Definition.Bytes == null || !inst.Definition.Bytes.TryGetValue(key, out var field))
                    throw new KeyNotFoundException($"Unknown parameter '{key}' for {inst.Definition.OpcodeName}");

                var type = field.Type ?? throw new Exception($"Missing type for param '{key}'");

                inst.Parameters[key] = ParseTypedValue(type, valStr);
            }

            // Also ensure all required params exist (don’t let user omit)
            foreach (var kv in inst.Definition.Bytes!)
            {
                if (kv.Key == "Opcode") continue;
                if (!inst.Parameters.ContainsKey(kv.Key))
                    throw new Exception($"Missing required param '{kv.Key}'");
            }
        }

        private static object ParseTypedValue(string type, string s)
        {
            // Accept: 0xFFFF, FFFF, -12, 123
            bool isHex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                         || (s.Any(char.IsLetter) && !s.StartsWith("-", StringComparison.Ordinal));

            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);

            // Signed/unsigned hex parsing must respect bit-width
            return type switch
            {
                // 8-bit
                "UCHAR" => isHex ? Convert.ToByte(s, 16) : Convert.ToByte(int.Parse(s)),
                "CHAR" => isHex
                            ? unchecked((sbyte)Convert.ToByte(s, 16))
                            : Convert.ToSByte(int.Parse(s)),

                // 16-bit
                "USHORT" => isHex ? Convert.ToUInt16(s, 16) : Convert.ToUInt16(int.Parse(s)),
                "SHORT" => isHex
                            ? unchecked((short)Convert.ToUInt16(s, 16))
                            : Convert.ToInt16(int.Parse(s)),

                // 32-bit
                "UINT" or "ULONG" => isHex ? Convert.ToUInt32(s, 16) : Convert.ToUInt32(long.Parse(s)),
                "INT" or "LONG" => isHex
                            ? unchecked((int)Convert.ToUInt32(s, 16))
                            : Convert.ToInt32(long.Parse(s)),

                _ => throw new NotSupportedException($"Unsupported type {type}")
            };
        }
        private string FormatParams(ScdInstruction inst)
        {
            if (inst.Definition.Bytes == null)
                return "";

            var keys = inst.Definition.Bytes.Keys.Where(k => k != "Opcode");

            return string.Join(" ", keys.Select(k =>
            {
                var field = inst.Definition.Bytes[k];
                var type = field.Type ?? "";
                var value = inst.Parameters[k];

                return _numberViewMode switch
                {
                    NumberViewMode.Decimal => $"{k}={Convert.ToInt64(value)}",
                    NumberViewMode.Hex => $"{k}={FormatHexByType(type, value)}",
                    _ => $"{k}={value}"
                };
            }));
        }
        private static string FormatHexByType(string type, object value)
        {
            // Format as underlying bits for signed types
            return type switch
            {
                "CHAR" => $"0x{unchecked((byte)Convert.ToSByte(value)):X2}",
                "UCHAR" => $"0x{Convert.ToByte(value):X2}",

                "SHORT" => $"0x{unchecked((ushort)Convert.ToInt16(value)):X4}",
                "USHORT" => $"0x{Convert.ToUInt16(value):X4}",

                "INT" or "LONG"
                         => $"0x{unchecked((uint)Convert.ToInt32(value)):X8}",
                "UINT" or "ULONG"
                         => $"0x{Convert.ToUInt32(value):X8}",

                _ => $"0x{Convert.ToUInt64(value):X}"
            };
        }


        /*
        private void GridOpcodes_UserDeletingRow(object? sender, DataGridViewRowCancelEventArgs e)
        {
            if (e.Row.DataBoundItem is not ScdInstructionRow row) return;
            var inst = (ScdInstruction)row.Tag!;
            _instructions.Remove(inst);
        }


        private void GridOpcodes_UserAddedRow(object? sender, DataGridViewRowEventArgs e)
        {
            var def = OpcodeDatabase.Get(0x00); // Nop
            if (def == null) return;

            var inst = new ScdInstruction
            {
                Opcode = 0x00,
                Definition = def,
                Parameters = new Dictionary<string, object>()
            };

            foreach (var kv in def.Bytes!)
            {
                if (kv.Key == "Opcode") continue;
                inst.Parameters[kv.Key] = GetDefaultValue(kv.Value.Type!);
            }

            _instructions.Add(inst);
        }*/
        private void GridOpcodes_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_isRefreshing)
                return;

            if (e.RowIndex < 0) return;

            if (gridOpcodes.Columns[e.ColumnIndex].Name != "Opcode")
                return;

            var row = (ScdInstructionRow)gridOpcodes.Rows[e.RowIndex].DataBoundItem!;
            var inst = (ScdInstruction)row.Tag!;

            var newOpcodeHex = row.Opcode;
            byte newOpcode = Convert.ToByte(newOpcodeHex, 16);

            var def = OpcodeDatabase.Get(newOpcode);
            if (def == null)
                throw new Exception($"Opcode {newOpcodeHex} not found");

            inst.Opcode = newOpcode;
            inst.Definition = def;
            inst.Parameters.Clear();

            foreach (var kv in def.Bytes!)
            {
                if (kv.Key == "Opcode") continue;
                inst.Parameters[kv.Key] =
                    GetDefaultValue(kv.Value.Type!);
            }

            RefreshGrid();
        }

        public static object GetDefaultValue(string type) => type switch
        {
            "UCHAR" or "CHAR" => (byte)0,
            "USHORT" or "SHORT" => (short)0,
            "UINT" or "ULONG" => 0u,
            "INT" or "LONG" => 0,
            _ => 0
        };

        private ScdInstruction CreateNopInstruction()
        {
            var def = OpcodeDatabase.Get(0x00); // NOP
            if (def == null)
                throw new Exception("NOP opcode (00) missing from JSON");

            var inst = new ScdInstruction
            {
                Opcode = 0x00,
                Definition = def,
                Parameters = new Dictionary<string, object>()
            };

            foreach (var kv in def.Bytes!)
            {
                if (kv.Key == "Opcode") continue;
                inst.Parameters[kv.Key] =
                    GetDefaultValue(kv.Value.Type!);
            }

            return inst;
        }

        //End of code that should be moved
        private void SetupGrid()
        {
            gridOpcodes.AutoGenerateColumns = false;
            gridOpcodes.Columns.Clear();
            gridOpcodes.AllowUserToAddRows = false;
            gridOpcodes.AllowUserToDeleteRows = false;

            // Add columns (make sure Name matches DataPropertyName)
            gridOpcodes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Offset",
                HeaderText = "Offset",
                DataPropertyName = "Offset",
                Width = 70,
                ReadOnly = true
            });

            var opcodeColumn = new DataGridViewComboBoxColumn
            {
                Name = "Opcode",
                HeaderText = "Opcode",
                DataPropertyName = "Opcode",
                Width = 80,
                FlatStyle = FlatStyle.Flat
            };

            opcodeColumn.Items.AddRange(
                OpcodeDatabase.Opcodes.Keys
                    .OrderBy(k => k)
                    .ToArray()
            );

            gridOpcodes.Columns.Add(opcodeColumn);

            /*
            gridOpcodes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Opcode",
                HeaderText = "Opcode",
                DataPropertyName = "Opcode",
                Width = 60,
                ReadOnly = true
            });*/

            gridOpcodes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "Name",
                DataPropertyName = "Name",
                Width = 140,
                ReadOnly = true
            });

            gridOpcodes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Params",
                HeaderText = "Params",
                DataPropertyName = "Params",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = false // ONLY editable column
            });

            gridOpcodes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Description",
                HeaderText = "Description",
                DataPropertyName = "Description",
                Width = 250,
                ReadOnly = true
            });

            // Hook events HERE (safe place)
            //gridOpcodes.UserAddedRow += GridOpcodes_UserAddedRow;
            //gridOpcodes.UserDeletingRow += GridOpcodes_UserDeletingRow;
            gridOpcodes.CellEndEdit += GridOpcodes_CellEndEdit;
            gridOpcodes.CellValueChanged += GridOpcodes_CellValueChanged;

        }

        private void DisplayInstructions(List<ScdInstruction> list)
        {
            _rows.Clear();

            int offset = 0;
            foreach (var inst in list)
            {
                var paramText = string.Join(" ",
                    inst.Parameters.Select(p => $"{p.Key}={p.Value}"));

                _rows.Add(new ScdInstructionRow
                {
                    Offset = offset,
                    Opcode = inst.Opcode.ToString("X2"),
                    //Name = inst.Definition?.OpcodeName ?? $"UNKNOWN_{inst.Opcode:X2}",
                    Name = inst.Definition?.OpcodeName ?? $"UNKNOWN_{inst.Opcode:X2}",
                    Params = paramText,
                    Description = inst.Definition?.OpcodeDescription ?? "",
                    Tag = inst
                });

                offset += OpcodeSizeCalculator.Calculate(inst.Definition);
            }

            gridOpcodes.DataSource = null;
            gridOpcodes.DataSource = _rows;

            if (gridOpcodes.Rows.Count > 0)
                gridOpcodes.Rows[0].Selected = true;
        }

        //OPEN SCD
        private void openScdToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "SCD files (*.scd)|*.scd"
            };

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            _currentScdPath = ofd.FileName;

            _instructions = ScdParser.Parse(
                File.OpenRead(_currentScdPath));

            RefreshGrid();
            SetStatus($"Loaded {_currentScdPath}");
        }
        //SAVE SCD
        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentScdPath))
            {
                saveSCDAsToolStripMenuItem_Click(sender, e);
                return;
            }

            ScdCompiler_2.Write(_currentScdPath, _instructions);
        }

        //Save SCD AS
        private void saveSCDAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "SCD files (*.scd)|*.scd",
                FileName = Path.GetFileName(_currentScdPath) ?? "script.scd"
            };

            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            _currentScdPath = sfd.FileName;
            ScdCompiler_2.Write(_currentScdPath, _instructions);
        }

        //Biohazard version select
        private void bIO1RE1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpcodeDatabase.LoadForVersion(BioVersion.Biohazard1);
            bIO1RE1ToolStripMenuItem.Checked = true;
            bIO15RE15ToolStripMenuItem.Checked = false;
            bIO2RE2ToolStripMenuItem.Checked = false;
            bIO3RE3ToolStripMenuItem.Checked = false;
        }

        private void bIO15RE15ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpcodeDatabase.LoadForVersion(BioVersion.Biohazard1_5);
            bIO1RE1ToolStripMenuItem.Checked = false;
            bIO15RE15ToolStripMenuItem.Checked = true;
            bIO2RE2ToolStripMenuItem.Checked = false;
            bIO3RE3ToolStripMenuItem.Checked = false;
        }

        private void bIO2RE2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpcodeDatabase.LoadForVersion(BioVersion.Biohazard2);
            bIO1RE1ToolStripMenuItem.Checked = false;
            bIO15RE15ToolStripMenuItem.Checked = false;
            bIO2RE2ToolStripMenuItem.Checked = true;
            bIO3RE3ToolStripMenuItem.Checked = false;
        }

        private void bIO3RE3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpcodeDatabase.LoadForVersion(BioVersion.Biohazard3);
            bIO1RE1ToolStripMenuItem.Checked = false;
            bIO15RE15ToolStripMenuItem.Checked = false;
            bIO2RE2ToolStripMenuItem.Checked = false;
            bIO3RE3ToolStripMenuItem.Checked = true;
        }

        private void addInstructionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _instructions.Add(CreateNopInstruction());
            RefreshGrid();
        }

        private void insertInstructionAboveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int i = SelectedInstructionIndex;
            if (i < 0) return;

            _instructions.Insert(i, CreateNopInstruction());
            RefreshGrid();
        }

        private void insertInstructionBelowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int i = SelectedInstructionIndex;
            if (i < 0) return;

            _instructions.Insert(i + 1, CreateNopInstruction());
            RefreshGrid();
        }

        private void deleteInstructionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int i = SelectedInstructionIndex;
            if (i < 0) return;

            _instructions.RemoveAt(i);
            RefreshGrid();
        }
        private void RefreshGrid()
        {
            _isRefreshing = true;

            _rows.Clear();
            int offset = 0;

            foreach (var inst in _instructions)
            {
                _rows.Add(new ScdInstructionRow
                {
                    Offset = offset,
                    Opcode = inst.Opcode.ToString("X2"),
                    Name = inst.Definition?.OpcodeName ?? $"UNKNOWN_{inst.Opcode:X2}",
                    Params = FormatParams(inst),
                    Description = inst.Definition?.OpcodeDescription ?? "",
                    Tag = inst
                });

                offset += OpcodeSizeCalculator.Calculate(inst.Definition);
            }

            gridOpcodes.DataSource = null;
            gridOpcodes.DataSource = _rows;

            _isRefreshing = false;
        }

        private void hexidecimalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _numberViewMode = NumberViewMode.Hex;

            hexidecimalToolStripMenuItem.Checked = true;
            decimalToolStripMenuItem.Checked = false;

            RefreshGrid();
        }

        private void decimalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _numberViewMode = NumberViewMode.Decimal;

            hexidecimalToolStripMenuItem.Checked = false;
            decimalToolStripMenuItem.Checked = true;

            RefreshGrid();
        }
    }
}
