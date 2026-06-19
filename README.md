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
        ├── Camera/      # Caméra RTS
        └── Editor/      # Outils éditeur
```

## Avancement

### Étape 1A — Caméra RTS ✅
- Vue isométrique orthographique (45°)
- Déplacement WASD / flèches, clamp carte 128×128
- Zoom molette fluide (DOTween)
- Rotation Q/E par paliers de 45° fluide (DOTween)
- Paramètres exposés dans l'Inspector via Odin (FoldoutGroup)

## Lancer le projet

1. Ouvrir le projet dans Unity 6
2. Menu **AirportSim → Create Airport Scene** pour générer la scène
3. Ouvrir `Assets/_Game/Scenes/Airport.unity`
4. Appuyer sur **Play**

## Contrôles caméra

| Action | Touche |
|--------|--------|
| Déplacement | WASD ou flèches |
| Rotation gauche | Q |
| Rotation droite | E |
| Zoom | Molette souris |
