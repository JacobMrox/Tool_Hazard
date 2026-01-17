using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tool_Hazard.Biohazard.MSG;

namespace Tool_Hazard.Forms
{
    public partial class ClassicREmsgTool : Form
    {
        private EncodingDictionary? _dict;
        private string? _dictPath;

        private string? _currentMsgPath;
        private byte[]? _currentMsgBytes;
        private bool _dirty;

        // Change this to wherever you ship the default encoding.xml
        private static string DefaultEncodingPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources","Biohazard", "encoding.xml");

        public ClassicREmsgTool()
        {
            InitializeComponent();
            HookEvents();
        }

        private void HookEvents()
        {
            mnuLoadXmlDictionary.Click += (_, __) => LoadDictionaryViaDialog();
            mnuOpenMsg.Click += (_, __) => OpenMsgViaDialog();
            mnuSaveMsg.Click += (_, __) => SaveMsg(overwrite: true);
            mnuSaveMsgAs.Click += (_, __) => SaveMsg(overwrite: false);

            txtMsg.TextChanged += (_, __) =>
            {
                if (_currentMsgBytes != null) // only consider dirty after something is loaded
                {
                    _dirty = true;
                    UpdateTitle();
                }
            };

            this.FormClosing += (_, e) =>
            {
                if (!PromptSaveIfDirty())
                    e.Cancel = true;
            };

            UpdateTitle();
        }

        private void EnsureDictionaryLoaded()
        {
            if (_dict != null) return;

            if (File.Exists(DefaultEncodingPath))
            {
                _dictPath = DefaultEncodingPath;
                _dict = EncodingDictionary.Load(_dictPath);
                return;
            }
            MessageBox.Show(this,
                "No encoding dictionary loaded and the default encoding.xml was not found.\n\n" +
                $"Expected default at:\n{DefaultEncodingPath}",
                "No Encoding Dictionary",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            LoadDictionaryViaDialog();
        }

        private void LoadDictionaryViaDialog()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Load XML Encoding Dictionary",
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            _dictPath = ofd.FileName;
            _dict = EncodingDictionary.Load(_dictPath);

            // Re-decode current msg if one is open
            if (_currentMsgBytes != null)
                txtMsg.Text = MsgCodec.Decode(_currentMsgBytes, _dict, stopAtZero: false);

            _dirty = false;
            UpdateTitle();
        }

        private void OpenMsgViaDialog()
        {
            if (!PromptSaveIfDirty())
                return;

            EnsureDictionaryLoaded();

            using var ofd = new OpenFileDialog
            {
                Title = "Open MSG",
                Filter = "MSG Files (*.msg)|*.msg|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            _currentMsgPath = ofd.FileName;
            _currentMsgBytes = File.ReadAllBytes(_currentMsgPath);

            txtMsg.Text = MsgCodec.Decode(_currentMsgBytes, _dict!, stopAtZero: false);

            _dirty = false;
            UpdateTitle();
        }

        private void SaveMsg(bool overwrite)
        {
            EnsureDictionaryLoaded();

            if (_currentMsgBytes == null && string.IsNullOrEmpty(_currentMsgPath))
            {
                // Nothing loaded yet, still allow Save As
                overwrite = false;
            }

            string? path = _currentMsgPath;

            if (!overwrite || string.IsNullOrEmpty(path))
            {
                using var sfd = new SaveFileDialog
                {
                    Title = "Save MSG",
                    Filter = "MSG Files (*.msg)|*.msg|All Files (*.*)|*.*",
                    FileName = string.IsNullOrEmpty(_currentMsgPath) ? "message.msg" : Path.GetFileName(_currentMsgPath)
                };

                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                path = sfd.FileName;
            }

            try
            {
                var bytes = MsgCodec.Encode(txtMsg.Text, _dict!);
                File.WriteAllBytes(path!, bytes);

                _currentMsgPath = path;
                _currentMsgBytes = bytes;
                _dirty = false;

                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    ex.Message,
                    "Save failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private bool PromptSaveIfDirty()
        {
            if (!_dirty)
                return true;

            var result = MessageBox.Show(this,
                "You have unsaved changes. Save now?",
                "Unsaved changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel)
                return false;

            if (result == DialogResult.Yes)
                SaveMsg(overwrite: true);

            // If they clicked Yes but cancelled the Save dialog, we’re still dirty.
            return !_dirty;
        }

        private void UpdateTitle()
        {
            var filePart = string.IsNullOrEmpty(_currentMsgPath) ? "No MSG" : Path.GetFileName(_currentMsgPath);
            var dictPart = string.IsNullOrEmpty(_dictPath) ? "Default dictionary" : Path.GetFileName(_dictPath);

            Text = $"Classic RE MSG Tool - {filePart} - {dictPart}" + (_dirty ? " *" : "");
        }
    }
}
