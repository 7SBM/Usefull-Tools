# DayZ Animation Tools — Blender Plugin (Vogle-Fixed für Blender 5.x)
## Stand: 08.06.2026 | Fixes by Vogelmensch1989

---

## Was ist das?

Blender-Plugin um eigene DayZ Waffen-Handanimationen (IK-Poses) zu erstellen.
Autor: Mrtea101 / JD — gefixt für Blender 4.x / 5.x Kompatibilität.

**Fixes die gemacht wurden:**
- `import bpy_types` entfernt (existiert in Blender 4/5 nicht mehr)
- Alle `bpy_types.Bone` / `bpy_types.PoseBone` Type-Annotations → `bpy.types.Bone` / `bpy.types.PoseBone`
- `bpy.utils.unregister_module()` entfernt (in Blender 4.0 gelöscht)

---

## Installation (einmalig)

1. Den **gesamten Ordner** `DayZAnimationPlugin_Voglefixed` kopieren nach:
   ```
   %AppData%\Roaming\Blender Foundation\Blender\5.1\scripts\addons\DayzAnimationTools\
   ```
   > Wichtig: Der Ordner muss `DayzAnimationTools` heissen (nicht `DayZAnimationPlugin_Voglefixed`)

2. Blender öffnen → Edit → Preferences → Add-ons → "DayZ" suchen → Haken setzen

---

## Workflow: Eigene Handanim für eine Waffe erstellen

### Schritt 1 — Master Rig laden
```
_AssetSamples\JD_Master_Rig (No IK Bones).blend
```
In Blender öffnen. Das ist das offizielle DayZ Player Skelett (Spine, Arme, Hände).

### Schritt 2 — IK Bones hinzufügen
In Blender oben in der 3D Viewport Toolbar:
`DayZ Animation Tools → Tools → Add Survivor IK Bones`

Fügt `LeftHandOrigin`, `RightHandOrigin`, `LeftForeArmDirection`, `RightForeArmDirection` hinzu.
**Ohne diesen Schritt funktioniert der Hand-Grip nicht.**

### Schritt 3 — Waffe importieren
Waffen-Mesh als FBX importieren (File → Import → FBX).
Referenz-FBX: `_AssetSamples\SVD\FBX (Weapon_Parts)\` — dort ist eine SVD aufgeteilt in Body/Bolt/Mag/Trigger.

Waffe so positionieren wie der Spieler sie halten würde (Griffstück auf Höhe der rechten Hand).

### Schritt 4 — IK Pose erstellen (Pose Mode)
- Armature auswählen → Tab → Pose Mode
- `LeftHandOrigin` bewegen bis die linke Hand greift
- `RightHandOrigin` bewegen bis die rechte Hand greift
- Finger-Knochen anpassen falls nötig
- **Nur Frame 0 braucht einen Keyframe** — das ist eine statische Grip-Pose

Fertige Referenz zum Abschauen:
```
_AssetSamples\SVD\JD_SVD_IK_&_w_States.blend   <- IK Pose + Weapon States kombiniert
_AssetSamples\Poses\Rifle\M4 Rifle IK.blend     <- M4 Grip als einfaches Beispiel
_AssetSamples\Poses\Pistol\Pistol_IK.blend      <- Pistol Grip
```

### Schritt 5 — Export aus Blender
`DayZ Animation Tools → Export → DayZ Anim (.txa)`

Export-Einstellungen:
- **Typ: Survivor IK 2h** (für Gewehre / beide Hände)
- **Typ: Survivor IK 1h** (für Pistolen / eine Hand)

Speichern als z.B. `MeineWaffe_IK.txa`

### Schritt 6 — DayZ Workbench konvertiert .txa → .anm
1. DayZ Tools Workbench öffnen (kommt mit DayZ Tools auf Steam)
2. `.txa` Datei importieren
3. Als `.anm` exportieren → `MeineWaffe_IK.anm`

### Schritt 7 — .asi Datei erstellen
Textdatei `MeineWaffe.asi` mit folgendem Inhalt:
```
$animsetinstance {
 #template "DZ/anims/workspaces/player/player_main/player_main.ast"
 #nparents 1
 #parent "DZ/anims/workspaces/player/player_main/player_main_rifle.asi"
 #ikpose "7SBM_WeaponPack/Animations/MeineWaffe/MeineWaffe_IK.anm"
 $animations {
 }
}
```
Für Pistolen: `#parent "...player_main_pistol.asi"` verwenden.

Fertige ASI Referenz:
```
_Referenz\JDsAnimationDemo\Animations\SVD\JD_Demo_SVD.asi
```

### Schritt 8 — DayZ Script: Waffe registrieren
In `7SBM_WeaponPack\scripts\4_World\ModItemRegisterCallbacks.c`:
```cpp
modded class ModItemRegisterCallbacks
{
    override void RegisterFireArms(DayZPlayerType pType, DayzPlayerItemBehaviorCfg pBehavior)
    {
        super.RegisterFireArms(pType, pBehavior);

        // Gewehr (2-händig):
        pType.AddItemInHandsProfileIK(
            "MeineWaffe_Base",                                         // Klassen-Name (Base!)
            "7SBM_WeaponPack/Animations/MeineWaffe/MeineWaffe.asi",   // ASI Datei
            pBehavior,
            "7SBM_WeaponPack/Animations/MeineWaffe/MeineWaffe_IK.anm", // IK Pose
            "DZ/anims/anm/player/reloads/AKM/w_AKM_states.anm"        // Vanilla Weapon States
        );
    }
};
```

Fertige Script-Referenz:
```
_Referenz\Script\ModItemRegisterCallbacks_EXAMPLE.c
```

### Schritt 9 — handAnimFile entfernen
In der `config.cpp` der Waffe: `handAnimFile = "...";` Zeile **komplett löschen** falls vorhanden.
Das neue System braucht das nicht mehr.

---

## Vanilla Weapon States (für .asi #parent und Script)

| Waffentyp | states.anm Pfad |
|---|---|
| Gewehr (AKM-Style) | `DZ/anims/anm/player/reloads/AKM/w_AKM_states.anm` |
| Pistole (FNP45-Style) | `DZ/anims/anm/player/reloads/FNP45/w_fnp45_states.anm` |
| Pistole (P1-Style) | `DZ/anims/anm/player/reloads/P1/w_P1_states.anm` |

---

## Ordnerstruktur dieser Ablage

```
DayZAnimationPlugin_Voglefixed\
├── ANLEITUNG.md                    <- Diese Datei
├── __init__.py                     <- Plugin Hauptdatei (gefixt)
├── Export\                         <- Export-Module (gefixt)
├── Import\                         <- Import-Module
├── Tools\                          <- Blender Tools (Add IK Bones, Model.cfg Generator)
├── Types\                          <- Interne Datentypen (txa/txo)
├── Utils\                          <- Parser Utilities
├── Images\                         <- Plugin UI Bilder
├── _AssetSamples\
│   ├── JD_Master_Rig (No IK Bones).blend   <- DayZ Player Rig (HIER STARTEN)
│   ├── Poses\                               <- Fertige IK Poses als .blend + .txa
│   │   ├── Rifle\M4 Rifle IK.blend          <- Gewehr Beispiel
│   │   ├── Pistol\Pistol_IK.blend           <- Pistol Beispiel
│   │   └── ...
│   └── SVD\                                 <- SVD Komplett-Beispiel
│       ├── JD_SVD_IK_&_w_States.blend       <- IK + States zusammen
│       ├── JD_SVD_Fire.blend                <- Feuer-Animation
│       └── FBX (Weapon_Parts)\              <- SVD FBX Meshes
└── _Referenz\
    ├── JDsAnimationDemo\                    <- Komplettes Demo-Mod Beispiel
    │   ├── Animations\SVD\JD_Demo_SVD.asi   <- Fertiges ASI Beispiel
    │   └── Scripts\                         <- Fertige Script-Struktur
    └── Script\
        └── ModItemRegisterCallbacks_EXAMPLE.c  <- Script Vorlage
```

---

## Häufige Fehler

| Fehler | Ursache | Fix |
|---|---|---|
| Falsche Halteposen / Schlagstock | Base-Klasse fehlt in `ModItemRegisterCallbacks` | Eintrag für `MeineWaffe_Base` hinzufügen |
| Hände greifen ins Leere | IK Bones nicht hinzugefügt (Schritt 2) | `Tools → Add Survivor IK Bones` |
| .asi nicht gefunden | Pfad in .asi oder Script falsch | Pfad prüfen — relativ zum Mod-Root |
| `handAnimFile` ignoriert | Altes System, nicht kompatibel | Zeile entfernen, neues System verwenden |
