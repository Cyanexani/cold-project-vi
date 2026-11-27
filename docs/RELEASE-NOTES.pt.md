Julia's Launcher 0.3.2-edupode: a 0.3.1 do felicienradio com as correções do edupode por cima.

Da 0.3.1 (felicienradio):
- O jogo volta a arrancar quando o nome do utilizador do Windows tem acentos: os caminhos da cache estragados no launcher.ini são corrigidos sozinhos, e com acentos na pasta do utilizador a cache de shaders passa para C:\VIctory\cache. Prepara os shaders mais uma vez depois de atualizar.
- Francês: um botão FR ao lado de EN e PT.

Correções do edupode a partir dos relatórios dos jogadores:
- Gráficas integradas (Intel Iris Xe / UHD, AMD Radeon Graphics): todas as placas passam a ser lidas por DXGI, não só as NVIDIA. Um PC só com gráfica integrada fica em Ultra baixo com Multidões e lutas, geração de frames e relógio de 60 FPS desligados, um aviso antes de JOGAR (jogar na mesma / cancelar) e uma linha amarela na página Jogar; Hardware e ID e os relatórios mostram o tipo de gráfica. (A memória desconhecida era lida como uma placa de 12 GB.)
- Saves do jogo: exporta o teu para um zip no Ambiente de trabalho, ou importa o de um amigo (o teu fica primeiro numa cópia de segurança). Hardware e ID > Saves do jogo.
- Placas de 8 GB: o "Multidões e lutas" deixa de pôr 2 GB de buffers do jogo em placas com menos de 11 GB. Enchia a VRAM e o jogo corria a cerca de 3 FPS.
- Uma pasta do jogo só com o executável (sem os ~100 GB de dados) é indicada antes de JOGAR; o jogo fechava logo.
- Os shaders só contam como preparados quando os do jogo foram mesmo preparados (uma pasta com 12 aparecia "pronta").
- Radeon: se o motor edupode fechar ao arrancar duas vezes seguidas (um "device removed" numa RX 6800), o launcher muda sozinho para o motor AMD do Belobug.
- O JOGAR espera enquanto o Preparar ainda prepara os shaders, e os passos só para NVIDIA não são tentados noutras placas.
- O motor edupode é o predefinido para todas as placas; o VIctory-Belobug-AMD do Belobug fica como alternativa para Radeon (todo o crédito para o Belobug).
- Mais núcleos do processador, ligado por defeito (+1 FPS no mundo aberto no PC do autor).
- Resolução de saída: 1080p, 1440p (2K) ou 2160p (4K).
- Enviar erros: "Enviar ao edupode" (uma caixa privada de relatórios), uma issue no GitHub já preenchida, ou o Discord; o teu ID de hardware fica copiado para colar no Discord.
- Acabou o falso aviso de "memória insuficiente" antes de JOGAR.
