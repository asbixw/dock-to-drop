# Sketchfab models to download

Sketchfab only gives downloads to a signed-in account, so these nine need you. Everything else (conversion,
cleanup, scaling, placing them in the game, credits) I do after you tell me they're downloaded.

All nine are licensed **CC Attribution (CC-BY)**. That means free use, including in a game you sell, as long as
the game credits the author. I'll add the credits to `THIRD_PARTY_NOTICES.md`. Total download is about 130 MB.

## Steps (the same for every model)

1. Create the folders once. Paste this into Terminal:
   ```bash
   mkdir -p ~/Downloads/d2d_props/{trailer_53ft,tractor_t680_scan,tractor_logistics,sedan_camry,pickup_f150,suv_tahoe,fuel_pump,gas_station_props,lot_light}
   ```
2. Open a link from the table below and sign in to Sketchfab (top right) if you aren't already.
3. Click **Download 3D Model** (the button under the 3D viewer).
4. In the window that opens, find the row **glTF** (labelled "Autoconverted format") and click its
   **DOWNLOAD** button. Don't pick "Original format", GLB or USDZ.
5. Double-click the downloaded zip in Finder. Move everything that comes out of it into that model's folder:
   usually `scene.gltf`, `scene.bin`, a `textures` folder and `license.txt`. Keep `license.txt`.
6. Repeat for the other eight, then tell me "downloads done".

If a model's download button is missing, or its license is no longer CC Attribution, skip it and tell me which
one. The backups at the bottom cover each item.

## The picks

| # | Folder | Used for | Model | Author | Faces | Size | Link |
|---|--------|----------|-------|--------|------:|-----:|------|
| 1 | `trailer_53ft` | Your trailer at door A-14 and the parked trailers | Truck Trailer FREE | NLM (NLM-Group) | 637k | 7 MB | https://sketchfab.com/3d-models/575958a53e3d4e9f8bcfb1ee2cef67bf |
| 2 | `tractor_t680_scan` | Parked rigs (truck plus trailer) | 18 Wheeler Truck /Tractor Trailer Photogrammetry | Air Digital (AirDigitalca) | 3.4M | 74 MB | https://sketchfab.com/3d-models/2208aa30e98f44c3a00549247b33a3ed |
| 3 | `tractor_logistics` | Parked rigs (truck only) | Logistics Semi-Truck - 3D Model | Nez1x75 | 60k | 17 MB | https://sketchfab.com/3d-models/cd48bf440b4d4f3a8a7c93b5926be663 |
| 4 | `sedan_camry` | Parked cars | Toyota Camry 2020 | ItsDiyor | 177k | 11 MB | https://sketchfab.com/3d-models/236a5a6e2fa6420fbdf641f4800cd544 |
| 5 | `pickup_f150` | Parked cars | 2015 Ford F150 King Ranch Edition | David_Holiday | 373k | 5 MB | https://sketchfab.com/3d-models/79ebd5caaf414ab08debc36157dbc878 |
| 6 | `suv_tahoe` | Parked cars | Chevrolet Tahoe 2007 | David_Holiday | 141k | 2 MB | https://sketchfab.com/3d-models/a11d407e141b4a88b2a424060075e357 |
| 7 | `fuel_pump` | Fuel dispensers | gas pump | kunkun (youoyouyou) | 13k | 13 MB | https://sketchfab.com/3d-models/add0f902b9164718bd0540e88bf9927e |
| 8 | `gas_station_props` | Fuel canopy | Gas station Props | Elbolillo (Elbolilloduro) | 13k | 3 MB | https://sketchfab.com/3d-models/8fcd52f1b12e4b4296589b4c6328b777 |
| 9 | `lot_light` | Light poles | Walmart Parking Lot Light w/ Gardco GL18 LED | streetlightshps | 5k | 1 MB | https://sketchfab.com/3d-models/06a632d7ac8f46fdac05f1c01a35665e |

## Why these, and what I'll change

1. **Trailer.** A clean white dry van with rear swing doors and no logos. I'll scale it to 53 ft (16.15 m)
   and cut it to about 60k faces.
2. **Photoscanned Kenworth T680 with trailer.** A 3D scan of a real parked rig, so it looks the most real of
   anything here. The trailer carries a small carrier logo. I'll cut it to about 150k faces and drop the
   scanned patch of ground.
3. **Silver T680-style tractor.** Game-ready and light. Its trailer has a big "Eagle Trans" graphic, so I'll
   use only the tractor.
4. **Camry.** The best-looking sedan with a download. It has a small "ItsDiyor" plate on the front, which I'll
   blank. I'll cut it to about 60k faces and tint copies different colors so the lot doesn't look cloned.
5. **F-150.** White, realistic, low-res textures (fine at parking-lot distance). Cut to about 60k faces.
6. **Tahoe.** Uses plain colored materials with no textures, which makes recoloring easy.
7. **Fuel dispenser.** A modern US pump with a made-up brand ("PROclean"). I'll shift its pink panels to
   green, the usual US diesel color.
8. **Gas station props.** A kit with a canopy, store and pumps laid out separately. I'll take the canopy and
   recolor its purple trim. No real brand on it.
9. **Lot light.** A US shoebox LED parking-lot pole. "Walmart" is only in the title; the model has no logo.

Two things I left out on purpose. The Volvo VNL by the Cascadia's author has a hammer-and-sickle and graffiti
livery. Every other free US tractor I found was untextured or a cartoon.

## Backups (only if a pick above fails)

| Item | Model | Author | Faces | Link |
|------|-------|--------|------:|------|
| Sedan | Honda Accord 2008 | David_Holiday | 124k | https://sketchfab.com/3d-models/1e3be91c28104f6489b42248884327fe |
| Sedan | Ford Fusion Sport | David_Holiday | 50k | https://sketchfab.com/3d-models/d43cf0e2d0e449b685aee3cd40e95c5f |
| Pickup | 2018 Chevrolet Silverado Centennial Edition | amogusstrikesback2 | 48k | https://sketchfab.com/3d-models/10c645c6477e4052b4ea91563ea539ce |
| SUV | 2021 Toyota RAV4 Hybrid | tonielpro520 | 172k | https://sketchfab.com/3d-models/de74fe4e98004c92b1103947597d1b53 |
| Canopy | Small Red Gas Station (67 MB) | SmashedBones | 617k | https://sketchfab.com/3d-models/1a0feafb044148e585bfe588afe617b8 |
| Light | Greenfield WI Parking Lot Light 2 | streetlightshps | 26k | https://sketchfab.com/3d-models/01667ca58fee4cbab39be7006f4cb0b2 |

Use the same folder name as the pick it replaces.
