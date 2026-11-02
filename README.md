# SEXTA-FEIRA ⚡

Otimizador de PC pra jogos (CS2, Valorant, Fortnite). Projeto privado do Gabriel.

## Instalar

1. Vai em **Releases** (lado direito do repositório) e baixa o `SEXTA-FEIRA-win-Setup.exe` da versão mais nova.
2. Roda o instalador. Se o Windows mostrar "O Windows protegeu o computador", clica em **Mais informações → Executar assim mesmo** (acontece porque o app ainda não tem assinatura digital).
3. Na primeira vez o app cria o botão **"Otimizar PC (SEXTA-FEIRA)"** no Menu Iniciar. Clica com o botão direito nele → **Fixar na barra de tarefas**.

Depois disso o app se atualiza sozinho.

## O que ele faz

- **Otimizar com 1 clique:** limpa temporários e lixeira, fecha launchers de fundo, liga Modo de Jogo, Alto Desempenho e otimizações para jogos em janela, desliga a gravação em segundo plano e libera a RAM em espera.
- **Painel ao vivo:** CPU, RAM, GPU e o que mais está pesando.
- **Checagem do PC:** XMP, dual channel, driver, jogo no HD, VBS, programas na inicialização, com botão de corrigir.
- **Jogos:** configs recomendadas e Modo Desempenho automático no Fortnite.
- **Desfazer tudo:** guarda o valor original de tudo que muda.

## Segurança

- Nunca mexe no processo dos jogos nem injeta nada (seguro com Vanguard, VAC, FACEIT e EAC).
- Só aplica ajustes com teste mostrando efeito. Tweaks de registro placebo ou que pioram o PC ficam de fora.

## Como atualizar o app

Toda mudança enviada pra `main` gera uma Release nova automaticamente (GitHub Actions + Velopack).
O secret `UPDATE_TOKEN` (token só de leitura deste repositório) precisa estar configurado em
**Settings → Secrets and variables → Actions** pros apps conseguirem baixar as atualizações.
