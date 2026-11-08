# Audio credits

All sound effects are cut, filtered and levelled from the real recordings below.
Every source is Creative Commons Zero (CC0 1.0), so attribution is not required,
but the authors deserve it anyway.

| Freesound ID | Recording | Author | Used for |
| --- | --- | --- | --- |
| [637916](https://freesound.org/s/637916/) | truck engine semi trailer idle deep close.flac | kyles | `engine_idle.wav` |
| [478133](https://freesound.org/s/478133/) | Truck-Diesel 10dodge start.mp3 | canucklovingbrit | `ignition.wav` |
| [465299](https://freesound.org/s/465299/) | Concrete Footsteps | SoftDistortionFX | `footstep_1.wav` … `footstep_6.wav` |
| [450225](https://freesound.org/s/450225/) | truck pickup door open close real nice slam.flac | kyles | `door_close.wav`, `fifth_wheel_lock.wav` |
| [361823](https://freesound.org/s/361823/) | Truck Door Open and Close | Jofae | `door_open.wav`, `glad_hand.wav` |
| [611460](https://freesound.org/s/611460/) | Creaking leather.wav | guitargeorge-2 | `seat.wav`, door-open creak |
| [504865](https://freesound.org/s/504865/) | air_brakes_loud_fade_out.wav | rabbydaw | `air_brake.wav`, `trailer_air.wav`, `glad_hand.wav` |
| [131930](https://freesound.org/s/131930/) | Industrial Air Horn | mcpable | `horn.wav` |
| [545024](https://freesound.org/s/545024/) | electric gate open.wav | gecop | `gate_motor.wav` |
| [376205](https://freesound.org/s/376205/) | Camera shutter.wav | jdaniel1999 | `camera_shutter.wav` |
| [138272](https://freesound.org/s/138272/) | Generic Ringtone.wav | Hupguy | `ring.wav` |
| [832628](https://freesound.org/s/832628/) | Calm Ambient Piano Loop | Jadis0x | `music_loop.wav` |
| [591529](https://freesound.org/s/591529/) | Ratchet Wrench Fast Multiple | Rudmer_Rotteveel | `crank_ratchet.wav` |

License text: <https://creativecommons.org/publicdomain/zero/1.0/>

The source MP3 previews are kept in `../AudioSource/` so the build is
reproducible with `python3 unity-assets/make_sfx_natural.py`.

## Dialogue

The dispatch, guard, receiver and coach lines are generated with Microsoft Edge
neural TTS (en-US-AndrewNeural, en-US-ChristopherNeural, en-US-JennyNeural and
en-US-AvaNeural). The generator is `voice/generate_natural.py`; it also applies a
light telephone band and gentle compression to the dispatch call.
