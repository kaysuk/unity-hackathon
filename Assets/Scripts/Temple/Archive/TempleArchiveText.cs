using UnityEngine;

internal static class TempleArchiveText
{
    internal const string SealedRegister = "Архивный код: СК-05 / № 07.";

    internal static readonly string[] Titles =
    {
        "Иерархия Шести",
        "Реестр поставок",
        "Протокол чёрной крови",
        "Каменное пророчество",
        "Дело Софьи Карамазовой",
        "Подготовка вместилища",
        "Закрытый реестр: Наследник",
    };

    internal static string ReadPage(int number) =>
        Resources.Load<TextAsset>($"TempleArchive/D{number:00}").text;
}
