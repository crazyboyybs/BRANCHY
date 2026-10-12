# BRANCHY

[English](#english) | [Português](#português)

## English

Open-source Git client for Windows, with WinUI 3, File Explorer integration,
visual history and implementation planning.

This distribution contains only the current BRANCHY sources and validation
project, without local development history, personal data, certificates or builds.
Git must be installed separately and available on PATH.

### Download and install

The first public version is **1.0**, with tag `v1.0` and MSIX version `1.0.0.0`.
Download the [x64 package](https://github.com/crazyboyybs/BRANCHY/releases/tag/v1.0)
and follow the [installation guide](branchy/docs/installing.md#english).

**Warning:** the initial package uses a self-signed test certificate. Installation
requires explicit consent to trust its public certificate; this is not a publicly
trusted signature. No private key is included in the downloads.

- [Features and build instructions](branchy/README.md#english)
- [Contributing](CONTRIBUTING.md#english)
- [Security](SECURITY.md#english)
- [MIT license](LICENSE)
- [Third-party notices](THIRD-PARTY-NOTICES.md#english)

### Build

Open `branchy/BRANCHY.slnx` in Visual Studio with the .NET and C++ desktop
development workloads. From Visual Studio's Developer PowerShell, at the
root of this distribution:

```powershell
msbuild .\branchy\BRANCHY.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x64
dotnet run --project .\branchy-validation\Branchy.Validation.csproj -c Release
```

A locally generated package needs signing before installation. The private key
is not included in the source; create your own test certificate or configure
an appropriate signing service. The manifest contains only the application's
technical development identity.

Read the [publication guide](branchy/docs/distributing-open-source.md#english)
before creating a release or a public repository.

---

## Português

Cliente Git open source para Windows, com WinUI 3, integração com o Explorer,
histórico visual e planejamento de implementações.

Esta distribuição contém apenas as fontes atuais do BRANCHY e sua validação,
sem o histórico local de desenvolvimento, dados pessoais, certificados ou builds.
O Git deve estar instalado separadamente e disponível no PATH.

### Baixar e instalar

A primeira versão pública é **1.0**, com tag `v1.0` e versão MSIX `1.0.0.0`.
Baixe o [pacote x64](https://github.com/crazyboyybs/BRANCHY/releases/tag/v1.0)
e siga o [guia de instalação](branchy/docs/installing.md#português).

**Atenção:** o pacote inicial usa certificado de teste autoassinado. A instalação
exige consentimento explícito para confiar no certificado público; não é uma
assinatura pública confiável. Nenhuma chave privada acompanha os downloads.

- [Funcionalidades e compilação](branchy/README.md#português)
- [Como contribuir](CONTRIBUTING.md#português)
- [Segurança](SECURITY.md#português)
- [Licença MIT](LICENSE)
- [Avisos de terceiros](THIRD-PARTY-NOTICES.md#português)

### Compilar

Abra `branchy/BRANCHY.slnx` no Visual Studio com os workloads .NET e C++ para
desktop. Pelo Developer PowerShell do Visual Studio, na raiz desta distribuição:

```powershell
msbuild .\branchy\BRANCHY.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x64
dotnet run --project .\branchy-validation\Branchy.Validation.csproj -c Release
```

Um pacote gerado localmente precisa de assinatura para instalação. A chave
privada não acompanha o código-fonte; crie seu próprio certificado de testes
ou configure o serviço de assinatura apropriado. O manifesto contém apenas
a identidade técnica de desenvolvimento do aplicativo.

Consulte [o guia de publicação](branchy/docs/distributing-open-source.md#português)
antes de criar uma release ou um repositório público.
