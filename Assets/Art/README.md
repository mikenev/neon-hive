# Nitrous Oxide: Art (pass 1)

Everything here is original, generated procedurally for this project. Nearly every asset is **white line art on a transparent background** (RGB = white, alpha = line plus glow). You tint it in the engine:
- Sprites: set `SpriteRenderer.color` (or UI `Image.color`) to the theme colour.
- Billboards or meshes: use an Unlit/Particles shader with additive or alpha blending, and set the material's base or emissive colour to the theme colour (HDR intensity 2–4 works well with Bloom).

Keep the background near-black (#04030A) and turn on URP Bloom for the neon look.

Sprites are 100 PPU, pivot at centre, no mipmaps, clamp wrap. Tunnel wall textures are Default type with Repeat wrap and mipmaps. The `.meta` files are included. Unity generates the OBJ metas.

## Themes

| Theme | Ring shape | Primary (rings) | Secondary (rails / accents) | Accent (FX / tough bugs) | Boss |
|---|---|---|---|---|---|
| **Ion Drift** | round (24-sided) | `#00F0FF` cyan | `#FF2BD6` magenta | `#3D7BFF` electric blue | Prism Moth |
| **Acid Hive** | hex (6) | `#9DFF00` acid lime | `#FFB300` amber | `#00FFA3` mint | Hive Queen |
| **Redline Core** | square (4) | `#FF2E4D` hot red | `#A15CFF` violet | `#FFE14D` volt yellow | Core Scarab |

To make bugs look tougher across themes, use the same sprites with a different tint: theme 1 cyan/blue, theme 2 lime/amber, theme 3 red/violet/yellow. You can also scale them up a little (1.1–1.2×) in later themes.

## Assets

### Tunnel (`Tunnel/`)
| Path | What / how |
|---|---|
| `ion_drift_wall.png`, `acid_hive_wall.png`, `redline_core_wall.png` | 256² seamless wireframe wall tiles: grid with ring seam, honeycomb, and forward-pointing chevrons with circuit traces. White on transparent. Tint per theme and scroll the material's V offset to fake forward speed. |
| `ion_drift_ring.obj` (24 sides), `acid_hive_ring.obj` (6), `redline_core_ring.obj` (4) | Open tube segment, radius 5, length 4 along +Z, normals facing inward. UV U wraps once per face (12× on the round ring) and V = 0..1 over the length, so the wall tile repeats once per face. Chain segments along Z or use one long scaled segment. If faces are culled from inside, set Render Face = Both on the material. |

### Ship (`Ship/`)
- `player_ship.png` (256²): delta-wing fighter seen from behind, nose up (+Y). Rotate it around the tunnel axis so +Y points toward the tunnel centre. Usually left white.

### Enemies (`Enemies/`, 256², white and tintable)
- `bug_mite.png`: swarm filler, small and fast.
- `bug_wasp.png`: diver that swoops along the wall.
- `bug_beetle.png`: armoured tank that takes several hits.
- `bug_spinner.png`: shooter. Spin it on Z and have it fire `fx_enemy_bullet`.

### Bosses (`Bosses/`, 512²)
- `boss_prism_moth.png`: Ion Drift boss. The two wing "eyes" make good weak points.
- `boss_hive_queen.png`: Acid Hive boss. The hex core in the abdomen is the weak point.
- `boss_core_scarab.png`: Redline Core boss. The diamond core in the centre is the weak point.

### Power-ups (`PowerUps/`, 128², hex badge plus icon)
- `pickup_spread.png`: 3-way spread (suggested tint `#FFE14D`).
- `pickup_laser.png`: piercing laser (`#00F0FF`).
- `pickup_rapid.png`: rapid fire (`#FF2BD6`).
- `pickup_homing.png`: homing (`#9DFF00`).

Put `FX/fx_pickup_glow.png` behind each pickup with the same tint and pulse its scale.

### FX (`FX/`)
- `fx_muzzle_flash.png` (128²): 8-point flash. Show it for 1–2 frames at the nose with a random Z rotation.
- `fx_bullet.png` (32×128): player bolt, pointing up (+Y).
- `fx_enemy_bullet.png` (64²): spiked orb. Tint red or the theme's primary colour.
- `fx_explosion_sheet.png` (1024×128): 8 frames of 128², sliced as `fx_explosion_sheet_0..7`. Play at about 20 fps.
- `fx_hit_spark_sheet.png` (256×64): 4 frames of 64², sliced as `fx_hit_spark_sheet_0..3`.
- `fx_pickup_glow.png` (128²): soft radial glow with a ring.

### UI (`UI/`)
- `ui_logo_nitrous_oxide.png` (1024×256): pre-coloured title (cyan "NITROUS", magenta "OXIDE"). `ui_logo_nitrous_oxide_white.png` is the same logo in white for tinting. Set in Orbitron (OFL, see `LICENSES/OFL-Orbitron.txt`). Only the rendered PNG ships; the font file isn't included.
- `ui_button.png`, `ui_button_highlight.png` (256×80): chamfered button, normal and highlighted. **9-slice border 24 px** on every side. Use `Image.type = Sliced`.
- `ui_panel.png` (256²): chamfered panel. **9-slice border 44 px** on every side.
- `ui_icon_life.png`, `ui_icon_score.png`, `ui_icon_powerup.png`, `ui_icon_pause.png` (64²): HUD icons (mini ship, gem, bolt, pause bars).
