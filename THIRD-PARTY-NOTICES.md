# Avisos de terceiros

A licença MIT do BRANCHY cobre o código próprio do aplicativo e do projeto
`branchy-validation`. Ela não substitui licenças de dependências ou de código
de terceiros. Os avisos abaixo devem acompanhar as distribuições pertinentes.

## Fluent UI System Icons

A fonte `Assets/Fonts/FluentSystemIcons-Regular.ttf` pertence ao projeto
[Microsoft Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons).
Seu aviso MIT é preservado em `Assets/Fonts/FluentSystemIcons-LICENSE.txt`.

## Recursos adaptados do WinUI

O template de ContentDialog em `Resources/DialogMaterials.xaml` foi adaptado
do WinUI da Microsoft. Seu aviso MIT é preservado em
`Resources/WinUI-LICENSE.txt`, e a atribuição permanece no próprio XAML.

## Dependências de compilação e execução

O projeto referencia Microsoft.WindowsAppSDK, Microsoft.Windows.SDK.BuildTools
e Microsoft.Windows.SDK.BuildTools.WinApp por NuGet. Seus termos e avisos
continuam sendo os fornecidos pelos respectivos pacotes; a licença do BRANCHY
não os substitui. Antes de publicar binários, revisar também os avisos de .NET,
Windows App SDK e componentes de runtime efetivamente incluídos no pacote.

O pacote Release 1.0 inclui o runtime .NET 10.0.12 e usa Windows App SDK 2.4.0.
Os termos e avisos desses pacotes estão preservados em `branchy/Resources/ThirdParty`,
incluídos no MSIX e também fornecidos na pasta `Licenses` do ZIP de instalação.

## Git

O BRANCHY executa o Git instalado pelo usuário como processo separado. A cópia
pública de fontes do BRANCHY não inclui nem compila o código-fonte do Git,
o `git-gui` Tcl/Tk ou um `git.exe` próprio.

O clone de desenvolvimento que contém o BRANCHY também contém o projeto Git:
esse código continua sujeito ao arquivo `COPYING` e aos avisos originais dos
respectivos componentes. Não remover seus copyrights nem relicenciá-lo como MIT.
Se uma distribuição futura incluir Git ou seu código, incluir e cumprir também
as licenças e obrigações correspondentes.
