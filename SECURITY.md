# Segurança e privacidade

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
