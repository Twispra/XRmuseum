namespace Museum.Localization
{
    /// <summary>
    /// 展馆体验支持的语言。
    ///
    /// 数值固定，不要重排：它们会随表资产和事件通道一起序列化。
    /// 追加新语言时**只在末尾添加**，避免已有资产指向错语言。
    /// </summary>
    public enum MuseumLanguage
    {
        /// <summary>简体中文；初始语言由 LanguageChannel 决定。</summary>
        Chinese = 0,

        /// <summary>English.</summary>
        English = 1,

        TraditionalChinese = 2,
    }

    public static class MuseumLanguageExtensions
    {
        /// <summary>该语言的原文名称，用于语言选择按钮的标签。</summary>
        public static string NativeName(this MuseumLanguage language)
        {
            return language switch
            {
                MuseumLanguage.Chinese => "简体中文",
                MuseumLanguage.TraditionalChinese => "繁體中文",
                MuseumLanguage.English => "English",
                _ => "?",
            };
        }

        /// <summary>表里带语言后缀的字段名，供校验工具报错时使用。</summary>
        public static string FieldName(this MuseumLanguage language)
        {
            return language == MuseumLanguage.Chinese ? "chinese" : language == MuseumLanguage.TraditionalChinese ? "traditionalChinese" : "english";
        }
    }
}
