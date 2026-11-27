Julia's Launcher 0.3.7: no more rainbow textures, Windows 10 starts the game, and your fastest drive.

- **Rainbow textures fixed.** Faces, shirts and walls full of coloured noise came from a texture that had not been read yet: its staging memory, reused from another texture, still held that one's bytes, and the card showed them. That memory is now emptied first, so a texture shows nothing for a moment and then the right picture. It costs a few milliseconds a frame while the world streams in. It was worst on 8 GB cards and 16 GB of RAM, where that memory is reused most.
- **Windows 10: the game starts.** Windows 10's own Direct3D 12 refused what the engine asks for, and the game never got past "D3D12 init". VIctory now comes with Microsoft's D3D12 Agility SDK (`D3D12\D3D12Core.dll`), which Windows 10 uses instead. On Windows 11 nothing changes. Launchers updated from 0.3.6 fetch it on their own, checked against its SHA-256.
- **Your fastest drive.** Shaders > **Move to another drive...** moves the shader cache (10-20 GB), the game's data and saves, the AMD engine and builds from source to any drive. Your saves are copied before they leave the old place.

Is it safe? [docs/SAFETY.md](https://github.com/felicienradio/cold-project-vi/blob/main/docs/SAFETY.md) · [Code signing policy](https://github.com/felicienradio/cold-project-vi/blob/main/docs/CODE-SIGNING-POLICY.md)
