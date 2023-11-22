using AdonisUI.ViewModels;
using EzASD.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace EzASD.ViewModels
{
    internal class EarlyWarningViewModel : PropertyChangedBase
    {
        private readonly EarlyWarningSign[] earlyWarningSigns;
        private string currentAge = "";

        public ObservableCollection<EarlyWarningSign> CurrentAgeWarningSigns { get; private set; }

        public ObservableCollection<string> AvailableAges { get; private set; }

        public string CurrentAge
        {
            get => currentAge;
            set
            {
                currentAge = value;
                CurrentAgeWarningSigns.Clear();
                earlyWarningSigns.Where(x => x.Age == currentAge).ToList().ForEach(x => CurrentAgeWarningSigns.Add(x));
                NotifyPropertyChanged();
            }
        }

        public EarlyWarningViewModel()
        {
            earlyWarningSigns = new EarlyWarningSign[]
            {
                new EarlyWarningSign("3m"  ,"对很大声音没有反应"),
                new EarlyWarningSign("3m"  ,"逗引时不发音或不会微笑"),
                new EarlyWarningSign("3m"  ,"不注视人脸，不追视移动人或物品"),
                new EarlyWarningSign("3m"  ,"俯卧时不会抬头"),
                new EarlyWarningSign("6m"  ,"发音少，不会笑出声"),
                new EarlyWarningSign("6m"  ,"不会伸手抓物"),
                new EarlyWarningSign("6m"  ,"紧握拳松不开"),
                new EarlyWarningSign("6m"  ,"不能扶坐"),
                new EarlyWarningSign("8m"  ,"听到声音无应答"),
                new EarlyWarningSign("8m"  ,"不会区分生人和熟人"),
                new EarlyWarningSign("8m"  ,"双手间不会传递玩具"),
                new EarlyWarningSign("8m"  ,"不会独坐"),
                new EarlyWarningSign("12m" ,"呼唤名字无反应"),
                new EarlyWarningSign("12m" ,"不会模仿“再见”或“欢迎”动作"),
                new EarlyWarningSign("12m" ,"不会用拇食指对捏小物品"),
                new EarlyWarningSign("12m" ,"不会扶物站立"),
                new EarlyWarningSign("18m" ,"不会有意识叫“爸爸”或“妈妈”"),
                new EarlyWarningSign("18m" ,"不会按照要求指人或物"),
                new EarlyWarningSign("18m" ,"与人无目光交流"),
                new EarlyWarningSign("18m" ,"不会独走"),
                new EarlyWarningSign("24m" ,"不会说3个物品的名称"),
                new EarlyWarningSign("24m" ,"不会按照吩咐做简单事情"),
                new EarlyWarningSign("24m" ,"不会用勺吃饭"),
                new EarlyWarningSign("24m" ,"不会扶栏上楼梯/台阶"),
                new EarlyWarningSign("30m" ,"不会说2-3个字的短语"),
                new EarlyWarningSign("30m" ,"兴趣单一、刻板"),
                new EarlyWarningSign("30m" ,"不会示意大小便"),
                new EarlyWarningSign("30m" ,"不会跑"),
                new EarlyWarningSign("36m" ,"不会说自己的名字"),
                new EarlyWarningSign("36m" ,"不会玩“拿棍当马骑”等假想游戏"),
                new EarlyWarningSign("36m" ,"不会模仿画圆"),
                new EarlyWarningSign("36m" ,"不会双脚跳"),
                new EarlyWarningSign("4y"  ,"不会说带形容词的句子"),
                new EarlyWarningSign("4y"  ,"不能按要求等待或轮流"),
                new EarlyWarningSign("4y"  ,"不会独立穿衣"),
                new EarlyWarningSign("4y"  ,"不会单脚站立"),
                new EarlyWarningSign("5y"  ,"不能简单叙说事情经过"),
                new EarlyWarningSign("5y"  ,"不知道自己的性别"),
                new EarlyWarningSign("5y"  ,"不会用筷子吃饭"),
                new EarlyWarningSign("5y"  ,"不会单脚跳"),
                new EarlyWarningSign("6y"  ,"不会表达自己的感受或想法"),
                new EarlyWarningSign("6y"  ,"不会玩角色扮演的集体游戏"),
                new EarlyWarningSign("6y"  ,"不会画方形"),
                new EarlyWarningSign("6y"  ,"不会奔跑"),
            };

            CurrentAgeWarningSigns = new ObservableCollection<EarlyWarningSign>();
            AvailableAges = new ObservableCollection<string>(earlyWarningSigns.GroupBy(x => x.Age).Select(x => x.Key));
        }
    }

    internal class Chat23ViewModel : PropertyChangedBase
    {
        private readonly Chat23Question[] chat23Questions;
        public ObservableCollection<Chat23Question> Chat23Questions { get; }
        public Chat23ViewModel()
        {
            chat23Questions = new Chat23Question[]
            {
                new Chat23Question("你的孩子喜欢你摇他或在你的膝上跳或类似的动作吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子喜欢和其他小孩玩耍吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子喜欢攀爬物体（比如爬楼梯、沙发）吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子喜欢玩躲猫猫或捉迷藏吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子喜欢玩假想游戏（比如，假装对着电话机说话，照顾娃娃或其他）吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子有没有用他的食指指着想要的东西（例如食物，玩具）吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子有没有用他的食指指着有兴趣的东西（例如汽车，飞机）？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会玩小件的玩具（比如，小汽车或积木），而并非咬、弄乱或扔掉它们吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会不会亲自拿东西给你（父母）看？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子看着你时，会不会望着你的眼睛最少一至两秒？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会对声音过分敏感（比如要掩着耳朵）吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子看到你的脸或看到你微笑时，会跟着你一起笑吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会模仿你的动作（比如模仿你的表情）吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("当你叫孩子的名字时，他 / 她会回应你吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("如果你指向房间另一边的玩具时，你孩子会看那玩具吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会走路吗？","是", "否"),
                new Chat23Question("你的孩子会留意你看着的物体吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子会不会无意识地在自己的脸旁边玩弄手指？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子有没有尝试吸引你注意他的活动？","没有", "偶尔","有时","经常"),
                new Chat23Question("你有没有怀疑过你的孩子有听力障碍？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子理解别人说的话吗？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子有没有漫无目的凝视某处或走来走去？","没有", "偶尔","有时","经常"),
                new Chat23Question("你的孩子当接触新的事物时，会不会看着你的表情去留意你的反应？","没有", "偶尔","有时","经常"),
            };

            Chat23Questions = new ObservableCollection<Chat23Question>(chat23Questions);
        }


    }

    internal class Chat23Question
    {
        public string Question { get; set; }

        public string[] Answers { get; set; }

        public int CheckedIndex { get; set; }

        public Chat23Question(string question, params string[] anwsers)
        {
            Question = question;
            Answers = anwsers;
        }
    }
}
