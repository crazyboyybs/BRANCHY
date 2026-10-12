# Publicar as fontes do BRANCHY

## Escopo e licença

O aplicativo em `branchy/` e a validação em `branchy-validation/` usam MIT para
o código próprio. O aplicativo chama o Git instalado, sem incorporar o código
do clone oficial. Os avisos Microsoft são preservados. O clone completo do Git
mantém sua própria licença: não publicá-lo inteiro sob o nome de licença MIT.

## Gerar uma cópia pública

Na raiz do clone de desenvolvimento:

```powershell
.\branchy\tools\Test-PublicSource.ps1
.\branchy\tools\Export-OpenSource.ps1
```

O exportador cria uma pasta nova e um ZIP em `dist/`. Ele inclui apenas tipos
de arquivo de fonte e assets conhecidos nas duas pastas do BRANCHY, mais os
documentos públicos. Não inclui `.git`, o código upstream do Git, workflows do
Git, builds, backups, logs, `.env`, certificados ou arquivos de usuário do IDE.
Também recusa links e verifica padrões comuns de segredos e caminhos pessoais.
Uma lista de hashes SHA-256 permite conferir os arquivos do snapshot.

O filtro e a busca por padrões são uma proteção adicional, não uma garantia
de detectar qualquer segredo. Revise o resultado antes de publicar. Arquivos
novos com extensões não permitidas exigem revisar a lista de inclusão.

## Histórico e identidade

O histórico de desenvolvimento original não é alterado. Não faça push desse
clone esperando que o `.gitignore` remova nomes, emails ou arquivos de commits
antigos. O snapshot permite iniciar um repositório separado, com uma identidade
pública/no-reply escolhida pelo mantenedor, sem transportar o histórico privado.
Não copie a pasta `.git` para a distribuição pública.

Não publicar a auditoria local, diretórios de backup, arquivos gerados pelo IDE
ou credenciais do ambiente. Se algum segredo já tiver vazado, revogue-o antes
de tratar sua remoção do histórico.

## Assinatura e Store

`CN=BRANCHY`, GUIDs, nomes de assembly e CLSIDs são identificadores técnicos
necessários, não credenciais. Não removê-los para anonimizar o projeto.
Thumbprints, passwords, arquivos de chave e caminhos de certificados reais
devem ficar em configurações privadas ou no serviço de assinatura.

O perfil público gera o pacote sem embutir uma chave de assinatura. Uma prévia
instalável pode ser assinada separadamente e acompanhar somente o `.cer` público,
com aviso e consentimento explícitos; nunca o `.pfx`. Para produção, configurar
assinatura confiável. A identidade de publicação precisa corresponder ao
certificado; a Store poderá exigir outra identidade, com migração de dados e
tratamento para evitar instalações duplicadas entre os canais.

Antes de publicar no GitHub, criar o repositório próprio do BRANCHY, ativar
private vulnerability reporting e revisar permissões de Actions. O exportador
não cria repositórios, não faz commits e não envia nada à rede.
