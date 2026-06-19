# AirportSim

Jeu de simulation de gestion d'aéroport en 3D low-poly, développé avec Unity 6.

## Style visuel

Low-poly isométrique — références : Airport CEO, Cities Skylines.

## Stack technique

| Outil | Version |
|-------|---------|
| Unity | 6000.0.5f1 |
| Render Pipeline | URP |
| Langage | C# |
| Inspector | Odin Inspector v3 |
| Animations/Tweens | DOTween v1.2.825 |
| Assets 3D | Synty Student Pack |

## Structure du projet

```
Assets/
└── _Game/
    ├── Materials/       # Matériaux du jeu
    ├── Scenes/          # Scènes Unity
    └── Scripts/
        ├── Camera/      # Caméra RTS + système de grille
        └── Editor/      # Outils éditeur (setup scène, grille)
```

## Avancement

### Étape 1A — Caméra RTS ✅ `v0.1.0`
- Vue isométrique orthographique (45°)
- Déplacement WASD / flèches, clamp carte 512×512
- Zoom molette fluide (DOTween)
- Rotation Q/E par paliers de 45° fluide (DOTween)
- Paramètres exposés dans l'Inspector via Odin (FoldoutGroup)

### Étape 1B — Grille au sol ✅ `v0.2.0`
- Grille 128×128 cellules de 4×4 unités (carte 512×512)
- Rendu GL.Lines visible en jeu, couleur blanche semi-transparente
- État par cellule : `Empty` / `Occupied` + enum `BuildingType`
- API publique : `GetCellFromWorldPos`, `IsCellAvailable`, `SetCellOccupied`
- Statistiques temps réel dans l'Inspector (Odin ShowInInspector)

### Étape 1C — HUD minimal ✅ `v0.3.0`
- Budget affiché en haut à gauche : `1 000 000 $`
- Horloge fictive en haut au centre : `06:00  x1`
- Touche T pour cycler la vitesse x1 / x2 / x4
- Canvas Screen Space Overlay, fond noir semi-transparent (TMP)
- EconomySystem et TimeManager en singleton avec API publique

## Lancer le projet

1. Ouvrir le projet dans Unity 6
2. Menu **AirportSim → Create Airport Scene** pour générer la scène
3. Menu **AirportSim → Add Grid System to Scene**
4. Sauvegarder (`Ctrl+S`)
5. Ouvrir `Assets/_Game/Scenes/Airport.unity` et appuyer sur **Play**

## Contrôles

### Caméra
| Action | Touche |
|--------|--------|
| Déplacement | WASD ou flèches |
| Rotation gauche | Q |
| Rotation droite | E |
| Zoom | Molette souris |

### Jeu
| Action | Touche |
|--------|--------|
| Cycler vitesse (x1 / x2 / x4) | T |
