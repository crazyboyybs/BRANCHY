# BRANCHY 1.0

Primeira versão pública do BRANCHY, cliente Git open source para Windows com
WinUI 3. Código próprio sob licença MIT, com avisos e licenças de terceiros preservados.

## Funcionalidades

- Abrir e clonar repositórios, inclusive do GitHub, com integração aos menus de contexto clássico e moderno do Explorer.
- Revisar diffs, localizar trechos, expandir a visualização e fazer edições rápidas.
- Preparar arquivos ou trechos, selecionar todas as alterações, descartar e restaurar arquivos.
- Criar commits, fazer amend com edição de mensagem, reverter commits, gerenciar branches, merges, conflitos, tags e remotos.
- Histórico visual com pesquisa por mensagem, diff ou branch e exportação de uma versão para ZIP.
- Editor interativo de gitignore e quadro de planejamento com progresso, tags, documentação e commits associados.
- Idiomas português e inglês, temas, layout ajustável, Mica e transições nativas.

## Downloads e instalação

`BRANCHY-1.0-windows-x64.zip` contém o MSIX bundle x64, a dependência do Windows
App Runtime, o certificado **público** de teste e instruções. O runtime .NET está
incluído; instale o Git separadamente. Consulte
[o guia de instalação](https://github.com/crazyboyybs/BRANCHY/blob/v1.0/branchy/docs/installing.md).

**Importante:** esta versão usa certificado de teste autoassinado, não uma
assinatura pública confiável. Confiar nele exige consentimento explícito e
permissão de administrador. Nenhuma chave privada, PFX, credencial ou histórico
local de desenvolvimento foi publicado.

A apresentação pública é **1.0**; o manifesto MSIX usa `1.0.0.0`. Instalações
locais anteriores `1.8.x.0` não são atualizadas por este pacote.

## Validação

Compilação Release x64, testes funcionais automatizados, testes de proteção da
publicação, inspeção do conteúdo MSIX e verificação da assinatura e dos hashes.
Essas verificações não substituem testes visuais nem testes em um Windows limpo.

Os ZIPs de código-fonte gerados pelo GitHub também estão disponíveis abaixo.
