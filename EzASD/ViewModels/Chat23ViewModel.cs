using AdonisUI.ViewModels;
using EzASD.Data.Entities;
using System;
using System.Collections.ObjectModel;
using System.Text;

namespace EzASD.ViewModels
{
    public class Chat23ViewModel : PropertyChangedBase
    {
        private readonly Chat23Question[] chat23Questions;

        private static string ToBase4(long base10Number)
        {
            var sb = new StringBuilder();
            do
            {
                sb.Append(base10Number % 4);
                base10Number /= 4;
            } while (base10Number != 0);

            char[] arr = sb.ToString().ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        public bool Calculator()
        {
            //一个特别烂的积分器
            var coreCount = 0;
            var count = 0;

            for (int i = 0; i < chat23Questions.Length; i++)
            {
                if (chat23Questions[i].Positive)
                {
                    count++;
                    if (i == 1 || i == 4 || i == 6 || i == 8 || i == 12 || i == 14 || i == 22)
                    {
                        coreCount++;
                    }
                }
            }

            if (coreCount >= 2 || count >= 6)
            {
                FriendlyResult = $"核心指标{coreCount}个失败，总计指标{count}个失败，筛查阳性！";
                NotifyPropertyChanged(nameof(FriendlyResult));
                return true;
            }
            else
            {
                FriendlyResult = $"通过！";
                NotifyPropertyChanged(nameof(FriendlyResult));
                return false;
            }
        }

        public string FriendlyResult { get; private set; } = string.Empty;

        public string AnswerHex { get; set; } = string.Empty;

        public Chat23aRecord ToChat23ARecord()
        {
            var answer = new StringBuilder();
            foreach (var question in chat23Questions)
            {
                answer.Append(question.CheckedIndex);
            }
            int base10Number = Convert.ToInt32(answer.ToString(), 4);
            string hexNumber = Convert.ToString(base10Number, 16);
            return new Chat23aRecord
            {
                Answer = hexNumber,
                Date = DateTime.Now,
            };
        }

        public void RemoteLoad()
        {
            long base10Number = Convert.ToInt64(AnswerHex, 16);
            string answer = ToBase4(base10Number);
            var offset = answer.Length - chat23Questions.Length;
            for (int i = 0; i < chat23Questions.Length; i++)
            {
                chat23Questions[i].CheckedIndex = i + offset < 0 ? 0 : Convert.ToInt32(answer[i + offset].ToString());
            }
        }

        public ObservableCollection<Chat23Question> Chat23Questions { get; }

        public Chat23ViewModel()
        {
            chat23Questions = new Chat23Question[]
            {
                new Chat23Question("你的孩子喜欢你摇他或在你的膝上跳或类似的动作吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子喜欢和其他小孩玩耍吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子喜欢攀爬物体（比如爬楼梯、沙发）吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子喜欢玩躲猫猫或捉迷藏吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子喜欢玩假想游戏（比如，假装对着电话机说话，照顾娃娃或其他）吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子有没有用他的食指指着想要的东西（例如食物，玩具）吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子有没有用他的食指指着有兴趣的东西（例如汽车，飞机）？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会玩小件的玩具（比如，小汽车或积木），而并非咬、弄乱或扔掉它们吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会不会亲自拿东西给你（父母）看？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子看着你时，会不会望着你的眼睛最少一至两秒？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会对声音过分敏感（比如要掩着耳朵）吗？",0,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子看到你的脸或看到你微笑时，会跟着你一起笑吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会模仿你的动作（比如模仿你的表情）吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("当你叫孩子的名字时，他 / 她会回应你吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("如果你指向房间另一边的玩具时，你孩子会看那玩具吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会走路吗？",0,"是", "否"),
                new Chat23Question("你的孩子会留意你看着的物体吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会不会无意识地在自己的脸旁边玩弄手指？",0,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子有没有尝试吸引你注意他的活动？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你有没有怀疑过你的孩子有听力障碍？",0,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子理解别人说的话吗？",3,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子有没有漫无目的凝视某处或走来走去？",0,"没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子当接触新的事物时，会不会看着你的表情去留意你的反应？",3,"没有", "偶尔","有时","经常"),
            };
            Chat23Questions = new ObservableCollection<Chat23Question>(chat23Questions);
        }

        public Chat23ViewModel(string answerHex) : this()
        {
            AnswerHex = answerHex;
        }

        public Chat23ViewModel(Chat23aRecord chat23aRecord) : this(chat23aRecord.Answer)
        {

        }
    }
}
