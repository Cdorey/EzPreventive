namespace EzNutrition.Shared.Data.DTO.PromptDto
{
    public class DialogConfiguration
    {
        public SecurityNotes SecurityNotes { get; set; } = new SecurityNotes();

        public string Purpose { get; set; } = "生成未来1个月内的饮食建议，使用自然语言生成答案，并提醒AI生成的信息务必仔细检查";

        public string Role { get; set; } = "你是营养科的医生助理";

        public string Environment { get; set; } = "医院营养门诊或社区健康咨询中心";
    }
}