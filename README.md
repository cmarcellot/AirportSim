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

### Étape 2A v3 — Zones dessinées & Pathfinding dual ✅ `v0.7.1`
- **11 types de zones dessinables** (glisser-poser sur la grille)
  - Airside : Runway · Taxiway · Apron
  - Terminal : TerminalHall · CheckInArea · SecurityArea · CustomsArea · BoardingLounge
  - Landside : Parking · RoadAccess · GreenArea
- **ZoneSystem** : grille `ZoneType?[,]` nullable, `SetZoneBatch`, event `OnZoneChanged`
- **ZoneData** (ScriptableObject) : nom, couleur, coût/cellule, matériau, flags airside/restricted
- **ZonePainter** : clic gauche = peindre, clic droit = effacer, preview temps réel (GPU instancing), coût affiché avant confirmation, `EconomySystem.TrySpend` à la validation
- **GroundRenderer** : rendu GPU instancié (`DrawMeshInstanced`) par type de zone, rebuild sur `OnZoneChanged`
- **PathfindingSystem** : deux graphes A* abonnés à `OnZoneChanged`
  - AirsideGraph : Runway + Taxiway + Apron
  - LandsideGraph : TerminalHall + CheckIn + Security + Customs + BoardingLounge
  - API : `FindAirsidePath` / `FindLandsidePath`
- **Menu de construction** : onglets **Zones** (peinture) | **Bâtiments** (pose) — 5 bâtiments : ControlTower · Gate · FuelStation · Shop · Restaurant

### Étape 2C — Taxi vers la gate ✅ `v0.9.0`
- **Gate** : état Available/Occupied, indicateur disque coloré (vert/rouge) flottant avec pulsation DOTween
- **Taxi PathfindingSystem** : après atterrissage, FlightScheduler cherche une gate libre, calcule le chemin via `FindAirsidePath()`, l'avion suit les waypoints cellule par cellule
- **Connexion taxiway→piste** : FlightScheduler peint automatiquement les cellules manquantes entre taxiways et pistes au démarrage (gap de 2 cellules comblé)
- **Rotation fluide** dans les virages (DORotateQuaternion + Ease.InOutSine)
- **Effets visuels** : disques moteurs en rotation continue (DOLocalRotate FastBeyond360), feux de navigation rouge/vert clignotants (DOTween Yoyo)
- La piste est libérée dès que l'avion commence à rouler → avion suivant peut atterrir

### Étape 2B — Premier avion ✅ `v0.8.0`
- **AircraftData** (ScriptableObject) : nom, vitesse approche/atterro/taxi, capacité, prefab
- **Aircraft** : machine à états `Approaching → Landing → Idle`, séquence DOTween complète
  - Spawn 500 u hors carte, descente alt 80 → 0, atterrissage + freinage sur piste
  - Trainée de fumée au toucher des roues (ParticleSystem burst)
  - Légère secousse caméra à l'atterrissage (`DOShakePosition`)
  - Disparaît après 90 s pour libérer la piste
- **FlightScheduler** : détecte les pistes via ZoneSystem, fait apparaître un avion toutes les 2 min de jeu (respecte `SpeedMultiplier`), `[Button] Spawn Test Aircraft`
- **Prefab Boeing 737** : corps + ailes + dérive (cubes blancs)

### Étape 4A — Passagers : spawn et pathfinding landside ✅ `v0.16.0`
- **Passenger.cs** : cube coloré 0.3×0.6×0.3 u (couleur = compagnie aérienne), états `Arriving → WaitingCheckin → CheckingIn → WaitingSecurity → PassingSecurity → WaitingGate → Boarding → Boarded`
- **Déplacement en 2 phases** : ligne droite depuis l'entrée de la route jusqu'à l'entrée du terminal (Z=-112), puis chemin A* landside `FindLandsidePath()` jusqu'à la zone check-in — vitesse 2 u/s, rotation fluide DOTween dans les virages
- **Satisfaction** : démarre à 80/100, décroît de 3 pts/s si l'attente en `WaitingCheckin` dépasse 60 s
- **PassengerSpawner.cs** : abonné à `FlightScheduler.OnFlightStatusChanged` — déclenche le spawn dès `FlightStatus.AtGate`, compte = `passengerCapacity × taux aléatoire [60 %-95 %]`, progression sur ~100 s réelles avec décalage latéral aléatoire
- **Chemin landside partagé** : calculé une seule fois par le Spawner et transmis à chaque passager (évite N appels A* simultanés)
- **Nettoyage vol** : les passagers sont détruits à `Departing` (embarquement terminé)
- **Odin Inspector** : état, satisfaction, vol assigné visibles sur chaque Passenger ; bouton « Rebuild Landside Path » sur le Spawner
- **Setup 4A** : menu `AirportSim → Setup 4A - Passengers` — ajoute `PassengerSpawner` à la scène

### Étape 3D — Passerelles d'embarquement (Jetways) ✅ `v0.15.0`
- **Jetway.cs** : composant rattaché à chaque Gate — états `Retracted / Extending / Docked / Retracting`
- **Animation DOTween** : rotation du pivot vers l'avion (0.4 s) puis extension fluide du bras (tube gris acier, tête de connexion blanche) via `DOVirtual.Float` sur la longueur locale en +Z
- **Coordination Gate** : `Gate.AssignAircraft()` → `Jetway.Extend(aircraft)` ; `Gate.ReleaseAircraft()` → `Jetway.Retract()` — découplage total, la Gate n'a qu'une référence optionnelle
- **Rétrocompatible** : `_jetway` est nul si aucun composant Jetway sur la Gate → aucune régression
- **Setup 3D** : menu `AirportSim → Setup 3D - Jetways` — ajoute un composant Jetway à chacune des 5 gates existantes
- **Odin Inspector** : `[ShowInInspector]` état et longueur du bras, `[Button]` « Test Extend » / « Test Retract »

### Étape 3C — Véhicule au sol : Bagagiste (Baggage Truck) ✅ `v0.14.0`
- **BaggageTruck** (hérite de GroundVehicle) : durée de service 4 min (temps de jeu), se gare à l'arrière de l'avion (opposé au nez, position calculée via `-aircraft.transform.forward`)
- **Animation hayon** : cube orange pivoté par DOTween (-90° autour de Z) à l'arrivée — s'ouvre comme une rampe de chargement ; se referme avant le retour au dépôt
- **Coordination triplex** : les trois trucks (Fuel + Catering + Baggage) servent l'avion en parallèle — départ bloqué tant que `fuelReady && cateringReady && baggageReady` ne sont pas tous vrais
- **Aircraft.cs** étendu : `SetBaggageReady(bool)`, visible dans l'Inspector Odin
- **Depot.cs** étendu : troisième pool (BaggageTrucks côté -Z derrière le dépôt), file d'attente indépendante, indicateur trois lignes
- **Rétrocompatible** : sans Depot, `BaggageReady` vaut `true` par défaut → pas de blocage
- **Setup 3C** : menu `AirportSim → Setup 3C - Baggage Truck` — crée le prefab BaggageTruck et injecte dans le Depot existant

### Étape 3B — Véhicule au sol : Catering Truck ✅ `v0.13.0`
- **CateringTruck** (hérite de GroundVehicle) : durée de service 3 min (temps de jeu), se gare côté opposé au FuelTruck (+X de la gate)
- **Animation plateforme** : cube bleu élévatrice monte de 3 unités via DOTween au début du service, redescend avant retour au dépôt
- **Coordination FuelTruck + CateringTruck** : les deux servent l'avion en parallèle — départ bloqué tant que `fuelReady && cateringReady` ne sont pas tous les deux vrais
- **Aircraft.cs** étendu : `SetFuelReady(bool)` / `SetCateringReady(bool)`, visibles dans l'Inspector Odin en mode lecture seule
- **Depot.cs** étendu : gère deux pools séparés (FuelTrucks côté -X, CateringTrucks côté +X), deux files d'attente indépendantes, indicateur mis à jour
- **Rétrocompatible** : sans Depot, `FuelReady` et `CateringReady` valent `true` par défaut → pas de blocage
- **Setup 3B** : menu `AirportSim → Setup 3B - Catering Truck` — crée le prefab CateringTruck et injecte dans le Depot existant

### Étape 3A — Véhicule au sol : Fuel Truck ✅ `v0.12.0`
- **GroundVehicle** : classe de base — vitesse configurable, suivi de chemin `List<Vector3>` via `PathfindingSystem.FindAirsidePath`, rotation fluide DOTween dans les virages, états `Idle / MovingToAircraft / Servicing / Returning`
- **FuelTruck** (hérite de GroundVehicle) : durée de service 2 min (temps de jeu), déclenché automatiquement quand `FlightStatus.AtGate`, barre de progression WorldSpace Canvas en face de la caméra, retour au dépôt après service
- **Depot** : bâtiment 2×2 cellules — 20 000 $, pool de 3 camions, file d'attente si tous occupés, indicateur flottant (camions disponibles / file)
- **Prefab FuelTruck** : corps jaune + citerne rouge (procédural)
- **Setup 3A** : menu `AirportSim → Setup 3A - Fuel Truck` — crée prefabs, BuildingData, pré-place un dépôt, ajoute au menu Construction

### Étape 2E — Planning basique des vols ✅ `v0.11.0`
- **FlightData** (ScriptableObject) : numéro, compagnie, couleur, AircraftData, heure d'arrivée/départ
- **FlightScheduler** réécrit : liste de vols planifiés (Odin), génération auto si liste vide, file d'attente si piste/gate indisponible, polling état avions, événements UI
- **FlightBoard** : panneau latéral droit togglé avec F, liste scrollable des vols (numéro, compagnie colorée, heure, gate, statut), animation DOTween slide
- **FlightNotificationSystem** : toasts en haut à droite (slide-in/fade-out DOTween) pour chaque événement de vol (en approche / à la gate / décollé)
- **Setup 2E** : menu `AirportSim → Setup 2E - Flight Planning`

### Étape 2D — Avion décollage ✅ `v0.10.0`
- **Cycle complet d'un vol** : AtGate 3 min (jeu) → Departing → TaxiingToRunway → TakingOff → Departed
- **Pushback** : DOTween recul depuis la gate (~20 u en sens inverse du cap d'arrivée)
- **Taxi vers la piste** : `PathfindingSystem.FindAirsidePath()` depuis la position post-pushback jusqu'au seuil de piste — même logique de suivi de waypoints que le taxi à l'arrivée
- **Roulement et décollage** : accélération 0→130 u/s, rotation nez vers le haut à V1 (80 u/s), montée alt 0→80, disparition hors carte
- **Gate libérée au départ** (pas à la destruction) → gate réutilisable immédiatement
- **Revenus** : +50 000 $ via `EconomySystem.AddRevenue()` au décollage
- **Notification UI** : "+50 000 $" vert gras TextMeshPro, montée + fondu DOTween sur le canvas HUD
- **Traînée moteur** : `ParticleSystem` continu (exhaust) pendant le roulement de décollage
- **Vibration caméra** à la rotation du nez (`DOShakePosition`)
- **AudioSource** placeholder pour les futurs sons moteurs
- **Odin Inspector** : `[ShowInInspector]` timer de gate + état, `[Button]` « Force Departure »

### Étape 2A-bis — Environnement de base prédéfini ✅ `v0.7.3`
- **AirportEnvironment** : génère automatiquement un layout d'aéroport au démarrage via ZoneSystem
  - Parking (20×16 cellules) en bas, relié au terminal par une route d'accès
  - TerminalHall central (40×30 cellules) avec murs blancs semi-transparents (hauteur 8 u)
  - Apron béton (30×20 cellules) derrière le terminal
  - Taxiways reliant l'apron aux pistes (L + R verticaux, horizontal de jonction)
  - Deux pistes parallèles en haut (60×8 cellules chacune)
- **Marquages au sol** : tirets blancs centraux + bandes de seuil sur les runways, ligne jaune continue sur les taxiways, grille blanche dans le parking
- **Éclairage URP** : lumière directionnelle 45°, ombres douces, ambiance bleu ciel
- **Odin Inspector** : [Button] « Regenerate Environment » / « Clear Environment », paramètres exposés (taille terminal, parking, nombre de pistes, largeur taxiway)

## Lancer le projet

1. Ouvrir le projet dans Unity 6
2. Menu **AirportSim → Create Airport Scene** pour générer la scène
3. Menu **AirportSim → Add Grid System to Scene**
4. Menu **AirportSim → Add HUD to Scene**
5. Menu **AirportSim → Setup 1D - Build System**
6. Menu **AirportSim → Setup 1E - Build Menu**
7. Menu **AirportSim → Setup 2A - Pathfinding**
8. Menu **AirportSim → Setup 2B - Flight System**
9. Menu **AirportSim → Setup 2C - Gates and Taxi**
9. Menu **AirportSim → Setup 2A-bis - Airport Environment**
10. Menu **AirportSim → Setup 2E - Flight Planning**
11. Menu **AirportSim → Setup 3A - Fuel Truck**
12. Menu **AirportSim → Setup 3B - Catering Truck**
13. Menu **AirportSim → Setup 3C - Baggage Truck**
14. Menu **AirportSim → Setup 3D - Jetways**
15. Menu **AirportSim → Setup 4A - Passengers**
16. Sauvegarder (`Ctrl+S`)
17. Ouvrir `Assets/_Game/Scenes/Airport.unity` et appuyer sur **Play**

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
