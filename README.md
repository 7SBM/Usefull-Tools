# P3D Debinarizer (VogelFixed 2026)

A simple GUI tool to debinarize P3D files used in DayZ / Arma.

---

## 🔧 Features

- GUI-based (no command line required)
- Select file → click → done
- Output is saved in the same directory as the source file
- **Debinarizes P3D files**
- **Converts ODOL (binarized) → MLOD (editable)**

---

## 🚀 Usage

1. Launch `p3d_Debinarizer.exe`
2. Select a `.p3d` file or folder
3. Click **OK / Convert**
4. The debinarized file will be saved automatically

---

## 🔄 What the tool does

### 1. Debinarization
- Removes the binary structure from P3D files

### 2. Conversion
- **ODOL → MLOD**
- Produces an editable P3D file

---

## 📸 Screenshots

### Folder structure before conversion
Shows a typical vehicle folder containing a binarized `.p3d` file.

![Before Conversion](/images/before.png)

---

### Tool in action (console output)
Displays the conversion process: loading ODOL and converting to MLOD.

![Console Output](/images/console.png)

---

### Result after conversion
The new `_mlod.p3d` file is created in the same directory.

![After Conversion](/images/after.png)

---

## 📁 Output

- Input: `model.p3d` (ODOL / binarized)
- Output: `model_mlod.p3d` (MLOD / editable)

---

## ⚠️ Notes

- Only works with supported P3D formats
- Some models may not convert perfectly (source limitation)
- Windows may block the executable (SmartScreen)

---

## 🧠 Credits / Attribution

This project is **based on and reworked from**:

https://github.com/Mekz0/P3D-Debinarizer-Arma-3/tree/master/Costura/bisdll

### Changes in this version:

- Reworked for **DayZ 1.29**
- Improved compatibility with newer P3D formats
- Adjusted behavior and stability
- Packaged as standalone EXE

---

## 🛑 Disclaimer

This tool is provided **as-is**, without warranty.

Use at your own risk.

---

## 👤 Author

Henry Meissner

---

## 📜 License

MIT License – see `LICENSE.md`
