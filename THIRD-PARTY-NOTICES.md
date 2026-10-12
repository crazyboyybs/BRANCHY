# Third-party notices / Avisos de terceiros

[English](#english) | [Português](#português)

## English

BRANCHY's MIT license covers original code in the application and
`branchy-validation` project. It does not replace dependency or third-party
licenses. The notices below must accompany the relevant distributions.

### Fluent UI System Icons

The `Assets/Fonts/FluentSystemIcons-Regular.ttf` font belongs to the
[Microsoft Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons)
project. Its MIT notice is preserved in `Assets/Fonts/FluentSystemIcons-LICENSE.txt`.

### Adapted WinUI resources

The ContentDialog template in `Resources/DialogMaterials.xaml` was adapted
from Microsoft's WinUI. Its MIT notice is preserved in
`Resources/WinUI-LICENSE.txt`, and attribution remains in the XAML itself.

### Build and runtime dependencies

The project references Microsoft.WindowsAppSDK, Microsoft.Windows.SDK.BuildTools
and Microsoft.Windows.SDK.BuildTools.WinApp through NuGet. Their terms and notices
remain those provided by their packages; BRANCHY's license does not replace them.
Before distributing binaries, also review the notices for .NET, Windows App SDK
and runtime components actually included in the package.

The 1.0 Release package includes .NET runtime 10.0.12 and uses Windows App SDK 2.4.0.
Their terms and notices are preserved in `branchy/Resources/ThirdParty`, included
in the MSIX, and also provided in the installation ZIP's `Licenses` folder.

### Git

BRANCHY runs the user's installed Git as a separate process. Its public source
distribution neither includes nor builds Git's source code, the Tcl/Tk
`git-gui` or a bundled `git.exe`.

The development clone containing BRANCHY also contains the Git project. That code
remains subject to its `COPYING` file and the original notices of its components.
Do not remove their copyrights or relicense them as MIT. If a future distribution
includes Git or its code, include and comply with the corresponding licenses
and obligations.

---

## Português

A licença MIT do BRANCHY cobre o código próprio do aplicativo e do projeto
`branchy-validation`. Ela não substitui licenças de dependências ou de código
de terceiros. Os avisos abaixo devem acompanhar as distribuições pertinentes.

### Fluent UI System Icons

A fonte `Assets/Fonts/FluentSystemIcons-Regular.ttf` pertence ao projeto
[Microsoft Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons).
Seu aviso MIT é preservado em `Assets/Fonts/FluentSystemIcons-LICENSE.txt`.

### Recursos adaptados do WinUI

O template de ContentDialog em `Resources/DialogMaterials.xaml` foi adaptado
do WinUI da Microsoft. Seu aviso MIT é preservado em
`Resources/WinUI-LICENSE.txt`, e a atribuição permanece no próprio XAML.

### Dependências de compilação e execução

O projeto referencia Microsoft.WindowsAppSDK, Microsoft.Windows.SDK.BuildTools
e Microsoft.Windows.SDK.BuildTools.WinApp por NuGet. Seus termos e avisos
continuam sendo os fornecidos pelos respectivos pacotes; a licença do BRANCHY
não os substitui. Antes de publicar binários, revisar também os avisos de .NET,
Windows App SDK e componentes de runtime efetivamente incluídos no pacote.

O pacote Release 1.0 inclui o runtime .NET 10.0.12 e usa Windows App SDK 2.4.0.
Os termos e avisos desses pacotes estão preservados em `branchy/Resources/ThirdParty`,
incluídos no MSIX e também fornecidos na pasta `Licenses` do ZIP de instalação.

### Git

O BRANCHY executa o Git instalado pelo usuário como processo separado. A cópia
pública de fontes do BRANCHY não inclui nem compila o código-fonte do Git,
o `git-gui` Tcl/Tk ou um `git.exe` próprio.

O clone de desenvolvimento que contém o BRANCHY também contém o projeto Git:
esse código continua sujeito ao arquivo `COPYING` e aos avisos originais dos
respectivos componentes. Não remover seus copyrights nem relicenciá-lo como MIT.
Se uma distribuição futura incluir Git ou seu código, incluir e cumprir também
as licenças e obrigações correspondentes.
