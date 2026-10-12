# Publishing sources / Publicar as fontes do BRANCHY

[English](#english) | [Português](#português)

## English

### Scope and license

The application in `branchy/` and validation in `branchy-validation/` use MIT
for original code. The application calls the installed Git without incorporating
the official clone's code. Microsoft notices are preserved. The full Git clone
retains its own license: do not publish the entire clone under MIT.

### Generate a public copy

From the development clone's root:

```powershell
.\branchy\tools\Test-PublicSource.ps1
.\branchy\tools\Export-OpenSource.ps1
```

The exporter creates a new folder and ZIP under `dist/`. It includes only known
source and asset file types from the two BRANCHY directories, plus public
documents. It excludes `.git`, upstream Git code and workflows, builds, backups,
logs, `.env`, certificates and IDE user files. It also rejects links and checks
common secret and personal-path patterns. A SHA-256 hash list lets you verify
the snapshot's files.

Filtering and pattern scanning provide additional protection, not a guarantee
that every secret will be detected. Review the result before publishing.
New files with unsupported extensions require reviewing the inclusion list.

### History and identity

The original development history is not modified. Do not push that clone expecting
`.gitignore` to remove names, emails or files from old commits. The snapshot allows
starting a separate repository with a public/no-reply identity chosen by the
maintainer, without carrying over private history. Do not copy `.git` into the
public distribution.

Do not publish the local audit, backup directories, IDE-generated files or
environment credentials. If a secret has already leaked, revoke it before
addressing its removal from history.

### Signing and Store

`CN=BRANCHY`, GUIDs, assembly names and CLSIDs are necessary technical identifiers,
not credentials. Do not remove them to anonymize the project. Thumbprints,
passwords, key files and actual certificate paths should remain in private
configuration or the signing service.

The public profile builds the package without embedding a signing key.
An installable preview may be signed separately and include only the public
`.cer`, with a warning and explicit consent; never a `.pfx`. Configure trusted
signing for production. The publishing identity must match the certificate;
the Store may require another identity, with data migration and handling to
avoid duplicate installations across channels.

Before publishing to GitHub, create BRANCHY's own repository, enable private
vulnerability reporting and review Actions permissions. The exporter does not
create repositories, make commits or send anything over the network.

---

## Português

### Escopo e licença

O aplicativo em `branchy/` e a validação em `branchy-validation/` usam MIT para
o código próprio. O aplicativo chama o Git instalado, sem incorporar o código
do clone oficial. Os avisos Microsoft são preservados. O clone completo do Git
mantém sua própria licença: não publicá-lo inteiro sob o nome de licença MIT.

### Gerar uma cópia pública

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

### Histórico e identidade

O histórico de desenvolvimento original não é alterado. Não faça push desse
clone esperando que o `.gitignore` remova nomes, emails ou arquivos de commits
antigos. O snapshot permite iniciar um repositório separado, com uma identidade
pública/no-reply escolhida pelo mantenedor, sem transportar o histórico privado.
Não copie a pasta `.git` para a distribuição pública.

Não publicar a auditoria local, diretórios de backup, arquivos gerados pelo IDE
ou credenciais do ambiente. Se algum segredo já tiver vazado, revogue-o antes
de tratar sua remoção do histórico.

### Assinatura e Store

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
