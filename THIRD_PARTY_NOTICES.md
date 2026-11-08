# Third-party notices

Dock to Drop is an independent project. The following open-source components and assets are used:

- **Three.js** — MIT License — https://threejs.org
- **Capacitor** — MIT License — https://capacitorjs.com
- **Barlow Condensed** — SIL Open Font License 1.1 — see `public/fonts/OFL-BarlowCondensed.txt`
- **Inter** — SIL Open Font License 1.1 — see `public/fonts/OFL-Inter.txt`
- **Playwright** — Apache License 2.0 (development/testing only) — https://playwright.dev
- **Vite** — MIT License — https://vitejs.dev

## Third-party 3D assets (Unity build)

| Asset in the game | Source | Author | License |
|---|---|---|---|
| Player truck (`unity-assets/Models/Freightliner_Cascadia.fbx`, `Textures/cascadia_*.png`) | This work is based on "FREIGHTLINER CASCADIA 2020" (https://sketchfab.com/3d-models/freightliner-cascadia-2020-2dd8eb87823c4eacabfb9d145eff5450) by liebherr996litronic (https://sketchfab.com/liebherr996litronic), licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/). Changed: original red paint and textures kept; decimated, tyres and rims given two-sided geometry, driver door cut out as a separate part, interior shell added (`unity-assets/build_truck_sketchfab.py`). | liebherr996litronic | CC-BY 4.0 (credit required) |
| Parked sedan (`Models/Props/Car_Camry.fbx`, `Textures/Props/SF_Car_Camry_*`) | This work is based on "Toyota Camry 2020" (https://sketchfab.com/3d-models/toyota-camry-2020-236a5a6e2fa6420fbdf641f4800cd544) by ItsDiyor (https://sketchfab.com/ItsDiyor), licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/). Changed: decimated to 32k triangles, textures resized; paint tinted per car (`unity-assets/build_vehicles.py`). | ItsDiyor | CC-BY 4.0 (credit required) |
| Parked SUV (`Models/Props/Car_Tahoe.fbx`, `Textures/Props/SF_Car_Tahoe_*`) | This work is based on "Chevrolet Tahoe 2007" (https://sketchfab.com/3d-models/chevrolet-tahoe-2007-a11d407e141b4a88b2a424060075e357) by David_Holiday (https://sketchfab.com/David_Holiday), licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/). Changed: scaled to real size, decimated to 30k triangles; paint tinted per car (`unity-assets/build_vehicles.py`). | David_Holiday | CC-BY 4.0 (credit required) |
| Parked sedan (`Models/Props/Car_Fusion.fbx`, `Textures/Props/SF_Car_Fusion_*`) | This work is based on "Ford Fusion Sport" (https://sketchfab.com/3d-models/ford-fusion-sport-d43cf0e2d0e449b685aee3cd40e95c5f) by David_Holiday (https://sketchfab.com/David_Holiday), licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/). Changed: scaled to real size, decimated to 30k triangles; paint tinted per car (`unity-assets/build_vehicles.py`). | David_Holiday | CC-BY 4.0 (credit required) |
| Parked pickup (`Models/Props/Car_Silverado.fbx`, `Textures/Props/SF_Car_Silverado_*`) | This work is based on "2018 chevrolet silverado centennial edition" (https://sketchfab.com/3d-models/2018-chevrolet-silverado-centennial-edition-10c645c6477e4052b4ea91563ea539ce) by amogusstrikesback2 (https://sketchfab.com/amogusstrikesback2), licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/). Changed: roof rack and headlight glow cards removed, scaled to real size, decimated (`unity-assets/build_vehicles.py`). | amogusstrikesback2 | CC-BY 4.0 (credit required) |
| Parked 53 ft trailers (`Models/Props/Trailer53.fbx`, `Textures/Props/SF_Trailer53_*`) | This work is based on "Truck Trailer FREE" (https://sketchfab.com/3d-models/truck-trailer-free-575958a53e3d4e9f8bcfb1ee2cef67bf) by NLM (https://sketchfab.com/NLM-Group), licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/). Changed: scaled to a 4.1 m height, middle section lengthened to 16.15 m (53 ft), widened to 2.59 m, recoloured white, decimated to 36k triangles (`unity-assets/build_vehicles.py`). | NLM | CC-BY 4.0 (credit required) |
| Diesel dispensers (`Models/Props/FuelPump.fbx`, `Textures/Props/SF_FuelPump_*`) | This work is based on "gas pump" (https://sketchfab.com/3d-models/gas-pump-add0f902b9164718bd0540e88bf9927e) by 打螺丝 (youoyouyou) (https://sketchfab.com/youoyouyou), licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/). Changed: decimated to 9k triangles, textures resized (`unity-assets/build_vehicles.py`). | 打螺丝 (youoyouyou) | CC-BY 4.0 (credit required) |
| Parked tractor (`Models/Props/Rig_Logistics.fbx`, `Textures/Props/SR_Log_*`) | This work is based on "Logistics Semi-Truck - 3D Model" (https://sketchfab.com/3d-models/logistics-semi-truck-3d-model-cd48bf440b4d4f3a8a7c93b5926be663) by Nez1x75 (https://sketchfab.com/Nez1x75), licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/). Changed: tractor only, converted to FBX and decimated (`unity-assets/build_props_sketchfab_rigs.py`). | Nez1x75 | CC-BY 4.0 (credit required) |
| Fuel canopy (`Models/Props/Canopy.fbx`, `Textures/Props/SR_*`) | This work is based on "Gas station Props" (https://sketchfab.com/3d-models/gas-station-props-8fcd52f1b12e4b4296589b4c6328b777) by Elbolillo (https://sketchfab.com/Elbolilloduro), licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/). Changed: canopy part only, converted to FBX (`unity-assets/build_props_sketchfab_rigs.py`). | Elbolillo | CC-BY 4.0 (credit required) |
| Lot light pole (`Models/Props/LotLight.fbx`, `Textures/Props/SR_*`) | This work is based on "Walmart Parking Lot Light w/ Gardco GL18 LED" (https://sketchfab.com/3d-models/walmart-parking-lot-light-w-gardco-gl18-led-06a632d7ac8f46fdac05f1c01a35665e) by AEL 115 & 315 Fan (streetlightshps) (https://sketchfab.com/streetlightshps), licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/). Changed: converted to FBX (`unity-assets/build_props_sketchfab_rigs.py`). | AEL 115 & 315 Fan (streetlightshps) | CC-BY 4.0 (credit required) |
| Driver body (`unity-assets/Models/Driver.fbx`, `Textures/driver_*.png`): Superhero_Male_FullBody, Hair_Buzzed, Hair_Beard, eye and skin textures | Universal Base Characters [Standard], https://quaternius.itch.io/universal-base-characters | Quaternius | CC0 1.0 |
| Driver animations (Idle, Walk, Jog, Driving, Interact, Sitting) | Universal Animation Library [Standard], https://quaternius.itch.io/universal-animation-library | Quaternius | CC0 1.0 |
| Tree_Oak_A / Tree_Oak_B / Tree_Small (+ LOD1) | Poly Haven `island_tree_01`, `island_tree_02`, `tree_small_02` (https://polyhaven.com/models) | Poly Haven contributors | CC0 1.0 |
| Shrub_A / Shrub_B | Poly Haven `shrub_02` | Poly Haven contributors | CC0 1.0 |
| Bush_A / Bush_B | Poly Haven `searsia_lucida` | Poly Haven contributors | CC0 1.0 |
| PowerPole | Poly Haven `modular_electricity_poles` | Poly Haven contributors | CC0 1.0 |
| JerseyBarrier, Hydrant, TrashCan, UtilityBox, Tyre, CardboardBox, CoveredCar | Poly Haven `concrete_road_barrier`, `fire_hydrant`, `metal_trash_can`, `utility_box_01`, `old_tyre`, `cardboard_box_01`, `covered_car` | Poly Haven contributors | CC0 1.0 |
| StreetLamp, StreetLampB, Barrel, BarrierB, SecurityLight | Poly Haven `street_lamp_01`, `street_lamp_02`, `barrel_03`, `concrete_road_barrier_02`, `security_light` (downloaded 2026-11-06) | Poly Haven contributors | CC0 1.0 |
| Ground textures `ph_asphalt`, `ph_concrete` (+ `_nor`) | Poly Haven `asphalt_07` (Charlotte Baglioni), `concrete_floor_worn_001` (Dimitrios Savva, Rico Cilliers), 2k JPG, downloaded 2026-11-06 (https://polyhaven.com/textures); regraded by `unity-assets/make_ground_ph.py` | Poly Haven contributors | CC0 1.0 |

The models were converted, decimated and re-dressed by `unity-assets/build_driver.py` and
`unity-assets/build_props.py`; textures are the 1k Poly Haven maps, repacked for Unity.
CC0 needs no attribution, but the authors are credited here anyway.

All other 3D models, textures, UI, audio synthesis, and game code were created for this project. No Amazon, Relay, Freightliner, Daimler Truck, or Telegram assets are included.

Brand names used in the game (LoadLink, TeleLink, Northgate Logistics, Gulf Coast Distribution) are fictional.

## Techniques referenced (no code copied)
- Third-person movement (body turns toward camera-relative input with a short smooth-damp, speed-driven
  locomotion blend): the approach of Unity's Starter Assets Third Person Controller
  (Unity Companion License). Re-implemented from the description in `PlayerController.cs` / `DriverBody.cs`;
  no Starter Assets code or assets are included.
- Follow camera that snaps in on collision and eases back out: John Nesky, "50 Game Camera Mistakes",
  GDC 2014.
- Truck model (bicycle model about the rear tandem, automated gearbox with torque curve, drag, body roll on
  a damped spring): general vehicle-sim write-ups such as Marco Monster's "Car Physics for Games" and Edy's
  Vehicle Physics documentation. Written from scratch in `TruckController.cs`.
