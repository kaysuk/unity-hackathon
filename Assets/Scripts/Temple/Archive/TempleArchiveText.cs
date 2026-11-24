using UnityEngine;

internal static class TempleArchiveText
{
    internal const string SealedRegister = "Архивный код: СК-05 / № 07. Для расшифровки соберите все семь записок.";

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

    internal static readonly string[] Reactions =
    {
        string.Empty,
        "У них даже люди проходят как поставки.",
        "Чёрная жидкость… Значит, дело не только в Мите.",
        "Они действительно собираются его призвать.",
        "Софья Карамазова. Мама…",
        "Она была здесь. И умерла после родов.",
        "Алексей. У нас есть брат. Они держали его здесь всё это время.",
    };
}
