# Instalar BRANCHY 1.0

## Requisitos

- Windows x64, versão 10 2004 (build 19041) ou posterior; Windows 11 para o menu de contexto moderno e Mica.
- Git instalado separadamente e disponível no PATH.
- Permissão de administrador somente para confiar no certificado de teste.

Baixe `BRANCHY-1.0-windows-x64.zip` da
[release oficial do projeto](https://github.com/crazyboyybs/BRANCHY/releases/tag/v1.0).
Extraia o ZIP antes de executar os comandos abaixo. O runtime .NET acompanha
o aplicativo; a dependência do Windows App Runtime acompanha o ZIP.

## Certificado de teste: consentimento obrigatório

Esta primeira distribuição é autoassinada, não possui assinatura pública
confiável e não foi publicada pela Microsoft Store. Confiar no certificado
permite instalar pacotes assinados pela mesma chave. Faça isso somente se
confiar neste projeto. Não importe o certificado em Trusted Root.

No PowerShell aberto na pasta extraída, confira o certificado:

```powershell
Get-PfxCertificate -FilePath .\BRANCHY-test.cer | Format-List Subject, Thumbprint, NotAfter
```

O subject deve ser `CN=BRANCHY` e a impressão digital SHA-1 deve ser
`A409E5C2061EA131E6FA2F5416016D8CF77002D0`. A impressão digital identifica
o certificado, não substitui os hashes SHA-256 dos downloads em `SHA256SUMS.txt`.

Se concordar, abra um PowerShell **como administrador** na mesma pasta e importe
somente o certificado público:

```powershell
Import-Certificate -FilePath .\BRANCHY-test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Não há PFX nem chave privada no download. Em seguida, no PowerShell da sua conta
normal, execute:

```powershell
Add-AppxPackage -Path .\BRANCHY-1.0-x64.msixbundle -DependencyPath .\Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix
```

Abra BRANCHY pelo menu Iniciar. Para desinstalar, use Configurações > Aplicativos.
Se não precisar mais do certificado de teste, remova apenas o certificado com a
impressão digital acima de Certificados do Computador Local > Pessoas Confiáveis.
Isso afeta a confiança de todas as contas do computador.

## Instalações anteriores de desenvolvimento

A versão pública começa em `1.0.0.0`. Pacotes locais de desenvolvimento numerados
`1.8.x.0` possuem uma versão maior e não são atualizados por este pacote. Não
desinstale automaticamente nem use uma opção de downgrade forçado: preserve
suas configurações e dados de planejamento antes de qualquer migração manual.
A compilação e a publicação desta versão não alteram a instalação local existente.

## Compilar por conta própria

O perfil Release inclui o runtime .NET, mantém o Windows App Runtime como
dependência MSIX e não inclui símbolos de depuração do BRANCHY. A assinatura
fica desativada no código público: use seu próprio certificado ou serviço de
assinatura. O publisher do certificado deve coincidir com `CN=BRANCHY` no manifesto.

Referência: [certificados para pacotes MSIX](https://learn.microsoft.com/en-us/windows/msix/package/create-certificate-package-signing).
