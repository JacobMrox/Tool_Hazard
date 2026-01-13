Written in C# Windows Forms .Net 8.0 (Not .Net Framework)

# Features

* Biohazard/Resident Evil 1-2-3:
  * Unpack/Repack RDT files (BIO1 not tested).
  * Support for EMD/PLD Unpacking/Repacking for RE1-2-3
  * SCD OpCode Editor (Only Biohazard 3 tested); with drag-drop updatable opcode database in the form of .json files
  * Ability to Installa Gemini-Loboto3's Classic Rebirth patch from the tool's interface.
* White Day (2001):
  * NOP file extraction (the repack functionality is a work in progress).
  * Supports extraction of files with Korean character file names without the need of Locale Emulator.
  * Font Editor.

# Coming Soon
* Live HUD feature for RE1-2-3
* Possible Intel.Orca Biorand implementation in-tool.

# Credits

* [White Day Font Editor](https://github.com/emuyia/wd-fonteditor) v1.2 by Emuiya.
* Reimplementation of [CRE-SCD-BHS Biohazard SCD Editor](https://github.com/3lric/CRE-SCD-BHS/) by 3lric in C#, with corrected opcode database for Biohazard 3/Resident Evil 3 (1999).
* Utilizes [Biohazard-utils](https://github.com/biorand/biohazard-utils/) .Net library by Intel.Orca for some file formats.
* Umbrella Corp [wallpaper](https://www.deviantart.com/grungestyle/art/umbrella-corp-wallpaper-v4-142492419) by GrungeStyle.
