# Security and privacy / Segurança e privacidade

[English](#english) | [Português](#português)

## English

Do not publish credentials, private keys, `.env`, development certificates,
memory dumps or raw logs in issues or pull requests. Diffs, paths and commit
messages can expose data from other projects. Use a disposable repository to
demonstrate problems.

For a vulnerability, use GitHub's **Report a vulnerability** feature when private
vulnerability reporting is enabled. If it is unavailable, ask maintainers for a
private channel without disclosing exploitable details or credentials in a
public issue.

Credentials that have already been published must be revoked or rotated.
Deleting the current file or adding it to `.gitignore` does not clean old commits.

BRANCHY uses Git and its configured credential helper. The application does not
include access tokens or a production signing key. Settings and planning data
are local; network operations requested by the user are executed by Git.

A self-signed certificate is for testing only. Installing its public certificate
changes Windows trust and requires explicit consent. Never publish a `.pfx` or
another copy of the private key alongside a release.

---

## Português

Não publique credenciais, chaves privadas, `.env`, certificados de
desenvolvimento, dumps de memória ou logs brutos em issues ou pull requests.
Diffs, caminhos e mensagens de commits podem revelar dados de outros projetos.
Use um repositório descartável para demonstrar problemas.

Para uma vulnerabilidade, use o recurso **Report a vulnerability** do GitHub
quando o repositório tiver private vulnerability reporting habilitado. Se ele
não estiver disponível, peça aos mantenedores um canal privado, sem divulgar
detalhes exploráveis ou credenciais em uma issue pública.

Credenciais que já tenham sido publicadas precisam ser revogadas ou rotacionadas.
Excluir o arquivo atual ou adicioná-lo ao `.gitignore` não limpa commits antigos.

O BRANCHY usa o Git e seu credential helper configurado. O aplicativo não inclui
tokens de acesso nem uma chave de assinatura de produção. Configurações e dados
de planejamento são locais; operações de rede solicitadas pelo usuário são
executadas pelo Git.

Um certificado autoassinado é apenas para testes. Instalar seu certificado
público altera a confiança do Windows e exige consentimento explícito. Nunca
publique um `.pfx` ou outra cópia da chave privada junto com uma release.
