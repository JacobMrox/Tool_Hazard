# Third-party notices — RE4 model viewer port

## JADERLINK_MODEL_VIEWER

MIT License — Copyright (c) 2024 JADERLINK
https://github.com/JADERLINK/JADERLINK_MODEL_VIEWER

Format research and decoder structure ported to `Biohazard/RE4/`:

- `ALL_VIEWER/SHARED_UHD_BIN_TPL/` (UHD BIN LE/BE, PS4/NS wide BIN + TPL, material 24-byte records, triangle strip/fan/quad decoding, `GLOBAL_POSITION_SCALE = 100`)
- `ALL_VIEWER/SHARED_GCWII_BIN/` (GC/Wii BE shorts, modern/alt-normal/vertex-color flags, 0x98/0x90/0x80/0xA0 face strips)
- `ALL_VIEWER/PS2_BIN/` + `RE4_PS2_MODEL_VIEWER/src/LoadPs2BinModel.cs` (PS2 VIF packets, 24-byte vertex lines, strip triangulation via IndexComplement)
- `ALL_VIEWER/RE4_UHD_MODEL_VIEWER/src/LoadUhdPack.cs`,
  `ALL_VIEWER/RE4_X360PS3_MODEL_VIEWER/src/LoadX360PS3Pack.cs`,
  `ALL_VIEWER/RE4_PS4NS_MODEL_VIEWER/src/LoadPs4NsPack.cs` (PACK tables: PackID, offsets, FFFFFFFF/PackID/Type header; DDS/TGA magic sniffing)
- `ALL_VIEWER/SHARED_SCENARIO_SMD/` + `ALL_VIEWER/LoadUhdBased/LoadUhdScenarioSMD.cs`
  (scenario header, 72-byte lines, embedded BIN/TPL offset tables)
- `ALL_VIEWER/SMX/` (144-byte SMX records, magic 0x10)
- `ALL_VIEWER/PMD_LIB/` + `ALL_VIEWER/RE4_2007_MODEL_VIEWER/src/LoadPmdModel.cs` (command-supported PMD + scenario PMD scaling)
- `ALL_VIEWER/StudioMdlDataReader/SmdReader.cs` (SMD_READER_LIB v1.1.1, Valve SMD reference https://developer.valvesoftware.com/wiki/SMD)

The OpenTK 3.3.3 renderer, MultiselectTree, camera, and shader loader were **not** ported;
Tool Hazard renders through its existing software `ModelViewport`.

### Other upstream credits (not copied; behavior referenced with attribution)

- OpenTK — https://opentk.net/ — MIT (see upstream Third_Party_License/OpenTK LICENSE.txt)
- CjClutter ObjLoader — https://github.com/chrisjansson/ObjLoader — MIT, reference only
  (Tool Hazard uses its existing `IntelOrca.Biohazard.WavefrontObjFile`)
- TGASharpLib — https://github.com/ALEXGREENALEX/TGASharpLib — MIT; subset reimplemented
- DDSReaderSharp — https://github.com/ALEXGREENALEX/DDSReaderSharp — MIT;
  based on DDSReader.java by Kenji Sasaki (MIT); subset reimplemented
- Scarlet — https://github.com/xdanieldzd/Scarlet — MIT; GNF only passes through raw

Full upstream license text (JADERLINK_MODEL_VIEWER, MIT):

```
MIT License

Copyright (c) 2024 JADERLINK

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
