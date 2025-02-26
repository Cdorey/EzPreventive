using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace EzNutrition.DataInserter
{
    /// <summary>
    /// 一些简单的脚本，用于向数据库追加原始数据集
    /// </summary>
    internal class Program
    {
        static void Main(string[] args)
        {
            AiAgency.Test.TestIt().Wait();
            Console.ReadLine();
        }


        public static void InserterUsage()
        {
            Console.WriteLine("ConnectionString:");
            var connectionString = Console.ReadLine();
            ////var optBuilder = new DbContextOptionsBuilder<EzNutritionDbContext>();
            ////optBuilder.UseSqlServer(connectionString).EnableSensitiveDataLogging().LogTo(Console.WriteLine, LogLevel.Information);
            ////var db = new EzNutritionDbContext(optBuilder.Options);
            ////var repo = new FoodNutritionValueRepository(db);
            ////var x = repo.GetFoods();
            ////Console.WriteLine("FilePath:");
            ////var filePath = Console.ReadLine();
            ////Values(db, filePath!);

            var optBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optBuilder.UseSqlServer(connectionString).EnableSensitiveDataLogging().LogTo(Console.WriteLine, LogLevel.Information);
            var db = new ApplicationDbContext(optBuilder.Options);

            db.Notices.Add(new Notice
            {
                Description = coverLetter,
                IsCoverLetter = true,
            });
            db.Notices.Add(new Notice
            {
                Description = notice,
            });
            db.SaveChanges();

        }

        static string coverLetter = @"# EzPreventive

### 公共卫生医师营养台账

您好，同行：

在工作之余，我开发了这个工具，旨在协助我们更便捷地进行食谱计算。这个工具具备自定义计算功能和营养分析，可以有效节省时间，提升我们的服务质量。

本工具源代码依照AGPL-3.0许可公开，如果您是注册的公共卫生执业医师，或者是注册在预防保健专业、儿童保健专业、妇女保健专业的临床执业医师，或者是工作在高校公共卫生学院的教职工，如有需要可通过邮件向我申请MIT许可。

我诚挚邀请您尝试使用这一工具，无论是日常工作中应用，还是分享给其他预防医学专业的同仁。目前，这一工具仍有一些不完善之处，尤其是自动生成的膳食建议可能还需进一步改进。在给患者或居民医学建议之前，请务必仔细核查相关信息的合理性。

此外，尽管营养专业长期以来一直属于预防医学范畴，但某些对公卫执业规则不认可的地区我们从事营养咨询和营养治疗工作可能存在违规风险。请务必确保您的行为不违反所在地区卫生行政部门的要求。

期待我们的合作能够为公共卫生事业带来更大的推动力。如有任何疑问或需要进一步信息和支持，请随时与我联系 [bsense@live.com](mailto:bsense@live.com)。

[主站地址](https://eznutrition.cdorey.net/)


祝愿您在工作中取得更卓越的成就！

*诚挚问候，*  
*CdoreyPoisson*
";

        static string notice = @"**更新内容：**

- 2025-02-11 更新了页面布局以适应即将增加的新功能模块
- 2025-02-14 增加24小时膳食回顾调查
- 2025-02-17 增加门诊咨询及多咨询对象档案切换支持

**提示：**

- EzNutrition服务器不会保存所有的咨询对象信息，未来也不会有此计划，因此请根据您的业务需要自行在您的电脑上保存。
- 计划在下一个版本增加营养SOAP病史及营养诊断报告和饮食建议辅助生成功能，考虑到这些功能需验证营养学专业资质，因此**预计当前为最后一个epiman临时账户无任何功能限制的版本**
- 预计可以验证的营养学专业资质包括：营养相关专业的医护人员、相关专业的高校教师及学生、在营养学会登记的注册营养师
- 计划重构膳食建议模块，计划引入DeepSeek API用于辅助拟定饮食教育内容
- 未来计划开发一个本地客户端程序，可以辅助在本地电脑中保存和管理咨询对象的数据，目前尚在构思中，优先级在计算和量表之后。";

        public static void Foods(EzNutritionDbContext db, string filePath)
        {
            using Workbook workbook = new Workbook(filePath!);
            foreach (var row in workbook.Rows)
            {
                var rec = new Food
                {
                    FriendlyCode = row[0].Trim(),
                    FriendlyName = row[1].Trim(),
                    EdiblePortion = string.IsNullOrWhiteSpace(row[2].Trim()) ? 100 : int.Parse(row[2].Trim()),
                    Details = row[3].Trim(),
                    FoodGroups = row[4].Trim(),
                    FoodId = Guid.NewGuid()
                };
                db.Foods!.Add(rec);
            }
            db.SaveChanges();
        }
        public static void Nutrients(EzNutritionDbContext db, string filePath)
        {
            using Workbook workbook = new Workbook(filePath!);
            foreach (var row in workbook.Rows)
            {
                var rec = new Nutrient
                {
                    FriendlyName = row[1].Trim(),
                    DefaultMeasureUnit = row[2].Trim(),
                    NutrientId = int.Parse(row[0].Trim())
                };
                db.Nutrients!.Add(rec);
            }
            db.SaveChanges();
        }
        public static void Values(EzNutritionDbContext db, string filePath)
        {
            using Workbook workbook = new Workbook(filePath!);
            var foods = db.Foods!.ToArray();
            foreach (var row in workbook.Rows)
            {
                var rec = new FoodNutrientValue
                {
                    NutrientId = int.Parse(row[1].Trim()),
                    Value = decimal.Parse(row[2].Trim()),
                    FoodId = foods.First(x => x.FriendlyCode == row[0].Trim()).FoodId,
                    FoodNutrientValueId = int.Parse(row[3].Trim())
                };
                db.FoodNutrientValues!.Add(rec);
            }
            db.SaveChanges();
        }
    }
}