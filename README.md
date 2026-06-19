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
    ├── Materials/            # Matériaux (sol, ghost valide/invalide, bâtiments)
    ├── Prefabs/Buildings/    # Prefabs des bâtiments (cube gris Runway…)
    ├── Scenes/               # Scènes Unity
    ├── ScriptableObjects/    # BuildingData assets (Runway.asset…)
    └── Scripts/
        ├── Buildings/        # BuildingData, BuildSystem, BuildingCategory
        ├── Camera/           # RTSCamera, GridSystem
        ├── Core/             # TimeManager
        ├── Economy/          # EconomySystem
        ├── UI/               # HUDController
        └── Editor/           # AirportSceneSetup (menus Unity)
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

### Étape 1D — Premier bâtiment placeable ✅ `v0.5.0`
- `BuildingData` ScriptableObject (nom, coût, taille, prefab, catégorie, type grille)
- Bâtiment de test : Runway 8×2 cellules, 80 000 $, cube gris allongé
- Ghost 3D suit la souris : vert = valide, rouge = invalide ou budget insuffisant
- Clic gauche = placer et déduire le coût · Clic droit = annuler
- Utilise `GridSystem.IsCellAvailable` + `EconomySystem.TrySpend`

### Étape 1E — Menu de construction complet ✅ `v0.6.0`
- 4 bâtiments : Runway, Terminal, Gate, Control Tower (prefabs cubes colorés)
- Menu en bas d'écran : icône couleur, nom, coût doré par bâtiment
- Onglets : Tous | Pistes | Terminaux | Services (filtre par catégorie)
- Bouton grisé si budget insuffisant, surligné si sélectionné
- Échap pour désélectionner · DOTween slide-from-bottom + punch au clic

### Étape 2A — Pathfinding au sol (taxiway) ✅ `v0.7.0`
- Bâtiment Taxiway (1×1, 5 000 $) ajouté au menu onglet Pistes
- `TaxiwayGraph` : graphe de nœuds A* sur les cellules Taxiway + Runway
- Reconstruction automatique toutes les 0,5 s si la grille change
- API publique : `FindPath(Vector3 start, Vector3 end) → List<Vector3>`
- Gizmos éditeur : nœuds verts, connexions blanches, chemin debug jaune
- Inspector : `NodeCount`, boutons "Rebuild Graph" et "Compute Debug Path"

### Étape 2A v2 — Zones & Pathfinding dual ✅ `v0.7.1`
- 15 bâtiments (6 Piste · 6 Terminal · 3 Accès) avec préfabs cubes colorés
- Menu de construction mis à jour : onglets **Tous / Piste / Terminal / Accès**
- `ZoneSystem` : classification Airside / Landside / Restricted par cellule, visualisation Gizmos colorés (bouton Toggle dans l'Inspector)
- `PathfindingSystem` : deux graphes A* indépendants
  - **AirsideGraph** : Runway, Taxiway, Apron, Gate, FuelStation, CargoArea
  - **LandsideGraph** : Terminal, Hall, CheckIn, Security, Shop, Restaurant, Parking, RoadAccess, BusStop, TaxiZone
- API : `FindAirsidePath(Vector3, Vector3)` et `FindLandsidePath(Vector3, Vector3)`
- Rebuild automatique toutes les 0,5 s si la grille change
- Gizmos : nœuds bleus (airside) / verts (landside), chemin debug jaune / orange

## Lancer le projet

1. Ouvrir le projet dans Unity 6
2. Menu **AirportSim → Create Airport Scene** pour générer la scène
3. Menu **AirportSim → Add Grid System to Scene**
4. Menu **AirportSim → Add HUD to Scene**
5. Menu **AirportSim → Setup 1D - Build System**
6. Menu **AirportSim → Setup 1E - Build Menu**
7. Menu **AirportSim → Setup 2A - Pathfinding**
8. Sauvegarder (`Ctrl+S`)
9. Ouvrir `Assets/_Game/Scenes/Airport.unity` et appuyer sur **Play**

## Contrôles

### Caméra
| Action | Touche |
|--------|--------|
| Déplacement | WASD ou flèches |
| Rotation gauche | Q |
| Rotation droite | E |
| Zoom | Molette souris |

### Jeu
| Action | Touche / Bouton |
|--------|-----------------|
| Cycler vitesse (x1 / x2 / x4) | T |
| Placer un bâtiment | Clic gauche |
| Annuler la sélection | Clic droit ou Échap |
