# Nitrous Oxide audio

All audio here is original, synthesized procedurally (oscillators, filtered noise, synthetic drums). It uses no samples, recordings or third-party material.
Format: OGG Vorbis, 44.1 kHz. Music is stereo and SFX are mono. Peaks are decoded values in dBFS, and nothing clips.

## Music (`Music/`)
Music files are seamless loops; set `AudioSource.loop = true`. Each file starts 20 ms before the downbeat so the loop point doesn't fall on a kick. Energy rises from Theme 1 to Theme 3 in tempo, layer density and drive.

| File | Length | Peak dBFS | Loop | Notes |
|---|---|---|---|---|
| `menu_loop.ogg` | 64.3 s | -3.1 | yes | Menu: 112 BPM, A minor; pads + arp, kick enters in bar 8 |
| `theme1_loop.ogg` | 67.5 s | -3.0 | yes | Theme 1: 128 BPM, E Dorian; four-on-the-floor, offbeat bass, arp |
| `theme2_loop.ogg` | 70.6 s | -3.2 | yes | Theme 2: 136 BPM, F# Phrygian; adds 16th hats, claps, resonant acid line |
| `theme3_loop.ogg` | 79.5 s | -2.9 | yes | Theme 3: 145 BPM, G harmonic minor; full kit with ride and snare fills, acid, arp, supersaw hook |

## SFX (`SFX/`)
SFX are one-shots (`loop = false`, or use `PlayOneShot`).

| File | Length | Peak dBFS | Loop | Use |
|---|---|---|---|---|
| `player_shot.ogg` | 0.16 s | -6.0 | no | Default player shot |
| `weapon_spread.ogg` | 0.24 s | -6.6 | no | Power-up weapon 1: spread (3 fanned zaps) |
| `weapon_laser.ogg` | 0.32 s | -5.8 | no | Power-up weapon 2: laser beam |
| `weapon_rapid.ogg` | 0.36 s | -5.7 | no | Power-up weapon 3: rapid fire |
| `weapon_homing.ogg` | 0.34 s | -5.9 | no | Power-up weapon 4: homing (lock-on chirp + whoosh) |
| `enemy_shot.ogg` | 0.20 s | -6.1 | no | Enemy shot |
| `bug_hit.ogg` | 0.10 s | -5.9 | no | Bug hit |
| `bug_explode.ogg` | 0.45 s | -6.3 | no | Bug explosion |
| `boss_hit.ogg` | 0.22 s | -6.1 | no | Boss hit |
| `boss_explode.ogg` | 2.40 s | -5.9 | no | Boss explosion (chain) |
| `boss_warning.ogg` | 1.70 s | -6.1 | no | Boss warning siren |
| `powerup_pickup.ogg` | 0.45 s | -5.5 | no | Power-up pickup |
| `player_hit.ogg` | 0.40 s | -6.1 | no | Player hit |
| `life_lost.ogg` | 1.30 s | -6.0 | no | Life lost |
| `extra_life.ogg` | 0.80 s | -6.0 | no | Extra life |
| `level_start.ogg` | 1.42 s | -6.0 | no | Level start |
| `level_clear.ogg` | 1.60 s | -6.0 | no | Level clear |
| `game_over.ogg` | 2.60 s | -6.1 | no | Game over |
| `menu_move.ogg` | 0.06 s | -6.2 | no | Menu move |
| `menu_select.ogg` | 0.24 s | -5.9 | no | Menu select |
