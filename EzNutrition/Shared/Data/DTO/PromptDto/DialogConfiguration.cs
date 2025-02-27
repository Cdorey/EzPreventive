namespace EzNutrition.Shared.Data.DTO.PromptDto
{
    public class DialogConfiguration
    {
        public SecurityNotes SecurityNotes { get; set; } = new SecurityNotes();

        public string Purpose { get; set; } = "生成未来1个月内的饮食建议，使用自然语言生成答案，对于不确定的或者不知道的内容可以直接标记此处你不了解，由你的调用者（医生）来判断，并提醒AI生成的信息务必仔细检查";

        public string Role { get; set; } = "你是专业但是谦逊的营养科医生助理";

        public string Environment { get; set; } = "医院营养门诊或社区健康咨询中心";

        /// <summary>
        /// 标明本次调用者的身份，例如“医生”、“患者”、“护士”等。
        /// </summary>
        public string CallerRole { get; set; }= "医生";
    }
}