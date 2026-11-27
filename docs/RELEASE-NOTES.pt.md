Julia's Launcher 0.3.9: a correção das texturas arco-íris a sério, a opção 720p já não fecha o jogo, e o Resizable BAR no driver da NVIDIA liga.

- **Texturas arco-íris, desta vez a sério.** A 0.3.7 anunciou a correção, mas ela ficou de fora dessa versão por engano: desculpem. Está na 0.3.9. A memória de cópia de uma textura, reutilizada de outra, é agora esvaziada antes de ser usada, por isso uma textura ainda não lida fica vazia um instante em vez de mostrar os bytes de outra.

- **O "720p" fechava o jogo ao arrancar (0.3.6 e 0.3.7).** O jogo para com o seu próprio erro fatal quando a consola diz que o ecrã é 1280x720, e o motor AMD do Belobug crashava com saída 720p. Todas as sessões a 720p acabaram assim. A opção passa a ser **Mundo a 720p (mais leve)**: o ecrã fica a 1080p e o mundo é desenhado a 720p no máximo. Se escolheste 720p, carrega outra vez em JOGAR.
- **"Resizable BAR no driver da NVIDIA" ficava vermelho.** O driver da NVIDIA só deixa administradores mudar as definições de Resizable BAR, por isso o "Ligar" era recusado sem aviso. O Windows pede agora permissões de administrador uma vez, e o passo fica verde.
- O anúncio no Discord liga à página desta versão (mostrava o cartão da versão anterior).

É seguro? [docs/SAFETY.md](https://github.com/felicienradio/cold-project-vi/blob/main/docs/SAFETY.md) · [Política de assinatura de código](https://github.com/felicienradio/cold-project-vi/blob/main/docs/CODE-SIGNING-POLICY.md)
