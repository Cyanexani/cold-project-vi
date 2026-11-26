# Estado do VIctory neste portatil

Atualizado pelo Claude a cada volta do ciclo. Para ver o jogo ao vivo, olha para a janela do
"VIctory teste em ciclo": de 15 em 15 s mostra FPS, ms por frame, desenhos e RAM livre (sem custo).

## Agora (2026-11-26 08:30)

- Versao: ramo `codex/cgbj-nvidia` = reupload cgbj (pareto-residency, afinado numa RTX 5090 com
  32 GB) + as nossas ferramentas. Shaders pre-traduzidos: 25 756 (229 nao traduzem; 26 deles sao
  shaders de 64 threads sem conversao para a NVIDIA, que antes eram recusados pelo driver).
- Definicoes: 1080p, buffers do jogo na placa (1-1536 MB), 64 listas no ar, texturas ate 3 GB na
  placa, niveis de texturas a pedido. Series X.
- A seguir: primeira volta do ciclo com esta versao; comparar FPS, crashes e capturas.

## Resultados por volta

| Hora | Versao / mudanca | FPS mediana (mundo) | Placa ms | Resultado |
|---|---|---|---|---|
| 04:46 | xtx, Series X, 4K | ~2,3 | ~200 | 1 h sem crash; ~20 % das tabelas recusadas |
| 06:24 | + 1080p, buffers na placa, 64 listas | 9,1 | 53 | sem crash; nevoa e objetos em falta |
| 08:21 | + VICTORY_WAVE_WIDE=1, 32 listas | - | - | placa perdida aos 1,7 min: descartado |

## O que ja se sabe

- A RTX aceita no maximo 1 000 000 descritores: as tabelas copiadas esgotam o espaco dinamico.
- 8 shaders de computacao (visibilidade, nevoa/volumetria) sao de 64 threads e a NVIDIA so corre 32.
- O metodo 0x1c8 nao e preciso em Series X (o jogo tem alternativa); em Series S a intro depende dele.
- Detalhes: `docs/nvidia-notes.md`, `docs/device-0x1c8.md`.
