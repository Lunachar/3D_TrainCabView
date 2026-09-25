# Generated 3D environment artwork

All images in `Generated/Environment` and the matching runtime copies in
`Resources/Cab3D/Environment` are original generated artwork for this project.
They are not copies of photographs or third-party game assets.

| Asset | Use in the route |
|---|---|
| `Station_Lenoblast_v1.png` | Northern regional station vista; architecture is generally inspired by the Vyborg/Priozersk area. |
| `Station_MuseumFantasy_v1.png` | Optional fantasy station-museum vista, without a franchise or trademark. |
| `Tunnel_TwoTrack_v1.png` | Distant two-track tunnel-exit backdrop. |
| `Crossing_RoadTraffic_v1.png` | Road-crossing / truck-route backdrop. |
| `City_Viaduct_v1.png` | City approach and viaduct backdrop. |
| `Station_NorthernRain_v1.png` | Rainy northern station vista with naturally placed umbrellas and wet-platform lighting. |
| `Road_ViaductTraffic_v1.png` | Parallel highway, cars, long-haul trucks, river and city viaduct at sunset. |
| `FoliageBillboardAtlas_Spring_v1.png` | Four transparent spring foliage cutouts for the 3D route: birch, maple and flowering shrubs. |
| `FoliageBillboardAtlas_Autumn_v1.png` | Four transparent autumn foliage cutouts for the 3D route. |
| `FoliageBillboardAtlas_Winter_v1.png` | Four transparent snow-covered foliage cutouts for the 3D route. |
| `Environment/CloudSpriteAtlas_v1.png` | Six transparent, softly lit cloud cutouts used by the moving 3D sky layer. |
| `Environment/DistantHills_v1.png` | Transparent panoramic blue-grey forested ridge for the far horizon, tinted for day, night and overcast weather; replaces the previous repeated spherical hill silhouettes. |
| `Environment/Terrain/MeadowGround_Spring_v1.png` | Seamless northern spring meadow albedo used on the route ground and roadside grass verges. |
| `Environment/Terrain/MeadowGround_Autumn_v1.png` | Seamless late-autumn grass and leaf-litter albedo for the ground and road verges. |
| `Environment/Terrain/MeadowGround_Winter_v1.png` | Seamless dormant grass with a light frost/snow cover for winter ground and road verges. |
| `Materials/StationBrick_v1.png` | Weathered northern red-brown brick albedo used on the physical station buildings. |
| `Materials/CityFacade_v1.png` | Warm-grey plaster and concrete albedo for city blocks and village houses. |

The game also uses a 3D track, tunnels, signals, roads, cars and trucks generated
from lightweight Unity geometry. The generated images are intentionally placed as
distant vistas and can be replaced by manually authored meshes in the editable
CabWorld3D prototype.

`Cab3DRouteAuthoring.ApplySeason` switches the foliage-billboard atlas at runtime.
The original `FoliageBillboardAtlas_v1.png` is the summer atlas.
# Additional original backdrops

- `Environment/Station_LenoblastRainy_v2.png` — original generated distant station vista: northern brick platform, wet weather, people and lights. Used only as a far background behind the editable 3D station geometry.

## Interior materials

- `Generated/Interior/CabSeatUpholstery_v1.png` — original tileable deep-blue woven train-seat fabric. Retained as an earlier visual variant.
- `Generated/Interior/CabSeatUpholstery_v2.png` — original seamless navy basket-weave upholstery with fine, logo-free fibers; assigned to the editable 3D seat material. Runtime copy: `Resources/Cab3D/CabSeatUpholstery_v2.png`.
- `Generated/Interior/CabMetalTrim_v1.png` — original brushed-metal and graphite powder-coat texture. The matching copy in `Resources/Cab3D` is assigned to the 3D cab shell, dashboard wings and lamp housings.
- `Generated/Interior/CabConsolePanel_v2.png` — original wide graphite console fascia with blank inset mounting plates and fine fasteners. The runtime copy in `Resources/Cab3D` is used beneath separate 3D controls, avoiding baked-in duplicate buttons and screens.

## Road materials

- `Generated/Materials/RoadAsphalt_v1.png` — seamless, top-down dark asphalt albedo with fine aggregate and restrained tire-wear variation (refreshed 2026-09-23; generated with the built-in image tool). Its matching runtime copy in `Resources/Cab3D` is tiled across rural roads, the level crossing, city viaduct and road overpass; painted markings remain separate geometry for crisp edges. Both import with anisotropic filtering for clearer detail at shallow viewing angles.
- `Generated/Materials/RoadAsphalt_v2.png` — higher-detail seamless charcoal asphalt albedo with natural fine aggregate and subtle tire wear. Used by the curved rural roads, crossing, viaduct and overpass; road markings remain separate geometry. Runtime copy: `Resources/Cab3D/RoadAsphalt_v2.png`.
- `Generated/Environment/Terrain/MeadowGround_Spring_v1.png`, `MeadowGround_Autumn_v1.png` and `MeadowGround_Winter_v1.png` — original seamless seasonal ground textures. Summer continues using `MeadowGround_v1.png`.
- `Generated/Materials/StationBrick_v1.png` and `CityFacade_v1.png` — original seamless wall albedo textures for the 3D station and low-rise buildings; windows and roofs remain separate geometry/materials.
- `Generated/Materials/CityFacade_v2.png` — original seamless, weathered light mineral-plaster albedo for the town buildings; replaces the low-detail concrete facade in the 3D viaduct district. Runtime copy: `Resources/Cab3D/CityFacade_v2.png`.

The 3D route layers seven low-cost, curve-following terrain regions over its base ground: meadow, forest, fields, village, mountain, town and lakeside. Each keeps a distinct material tint while `Cab3DRouteAuthoring.ApplySeason` swaps in the matching summer, spring, autumn or winter ground texture.
