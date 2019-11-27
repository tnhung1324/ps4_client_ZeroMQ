using NetMQ.Sockets;
using NetMQ;
using ZeroMQ;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ps4_client_ZeroMQ
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            Form.CheckForIllegalCrossThreadCalls = false;
            InitializeComponent();
            InitJoy();
        }
        

        //REF http://www.cnblogs.com/kingthy/archive/2009/03/25/1421838.html
        //REFVhttps://yal.cc/c-sharp-joystick-tracking-via-winmm-dll/
        [StructLayout(LayoutKind.Sequential)]
        public struct JOYCAPS
        {
            public ushort wMid;
            public ushort wPid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public int wXmin;
            public int wXmax;
            public int wYmin;
            public int wYmax;
            public int wZmin;
            public int wZmax;
            public int wNumButtons;
            public int wPeriodMin;
            public int wPeriodMax;
            public int wRmin;
            public int wRmax;
            public int wUmin;
            public int wUmax;
            public int wVmin;
            public int wVmax;
            public int wCaps;
            public int wMaxAxes;
            public int wNumAxes;
            public int wMaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szRegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szOEMVxD;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct JOYINFOEX
        {
            public Int32 dwSize; // Size, in bytes, of this structure.
            public Int32 dwFlags; // Flags indicating the valid information returned in this structure.
            public Int32 dwXpos; // Current X-coordinate.
            public Int32 dwYpos; // Current Y-coordinate.
            public Int32 dwZpos; // Current Z-coordinate.
            public Int32 dwRpos; // Current position of the rudder or fourth joystick axis.   right_v
            public Int32 dwUpos; // Current fifth axis position.  L2?
            public Int32 dwVpos; // Current sixth axis position.  R2?
            public Int32 dwButtons; // Current state of the 32 joystick buttons (bits)
            public Int32 dwButtonNumber; // Current button number that is pressed.
            public Int32 dwPOV; // Current position of the point-of-view control (0..35,900, deg*100)
            public Int32 dwReserved1; // Reserved; do not use.
            public Int32 dwReserved2; // Reserved; do not use.
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct JOYINFO
        {
            public Int32 wXpos; // Current X-coordinate.
            public Int32 wYpos; // Current Y-coordinate.
            public Int32 wZpos; // Current Z-coordinate.
            public Int32 wButtons; // Current state of joystick buttons.            
        }
        [DllImport("winmm.dll")]
        public static extern Int32 joyGetPos(Int32 uJoyID, ref JOYINFO pji);
        [DllImport("winmm.dll")]
        public static extern Int32 joyGetPosEx(Int32 uJoyID, ref JOYINFOEX pji);
        [DllImport("winmm.dll")]
        public static extern int joyGetDevCaps(int uJoyID, ref JOYCAPS pjc, int cbjc);

        public struct DeviceJoyInfo
        {
            public bool JoyEx;
            public int ButtonCount;
            public int ID;
            public int Button_old;
            public int Way_X_old;
            public int Way_Y_old;
            public int Way_Z_old;
            public int Way_R_old;
            public int Way_L2_old;
        }

        public struct joystickEvent
        {
            public int event_type; //0:方向鍵觸發 1:一般按鈕觸發
            public int joystick_id;//發生於哪個遊戲手把

            public int button_id;//如果是一般按鈕觸發,發生在哪顆按鈕
            public int button_event;//0:鬆開 1:壓下

            public int way_type; //0:x方向鍵盤 1:y方向鍵盤  2:X   
            public int way_value;
        }

        List<DeviceJoyInfo> joyinfo_list = new List<DeviceJoyInfo>();
        JOYCAPS joycap = new JOYCAPS();
        JOYINFO js = new JOYINFO();
        JOYINFOEX jsx = new JOYINFOEX();
        int JOYCAPS_size;
        private Label X_loc;
        private Label Y_loc;
        int PeriodMin = 0;

        public unsafe void InitJoy()
        {
            Stopwatch st = new Stopwatch();
            st.Restart();
            JOYCAPS_size = Marshal.SizeOf(typeof(JOYCAPS));

            for (int i = 0; i < 256; i++)
            {
                if (joyGetDevCaps(i, ref joycap, JOYCAPS_size) == 0)
                {
                    DeviceJoyInfo info = new DeviceJoyInfo();

                    //set id
                    info.ID = i;

                    //check joyex
                    if (joyGetPosEx(i, ref jsx) == 0)     //獲取遊戲設備的座標位置和按鈕狀態
                    {
                        info.JoyEx = true;
                        info.Way_X_old = jsx.dwXpos;
                        info.Way_Y_old = jsx.dwYpos;
                        info.Way_Z_old = jsx.dwZpos;
                        info.Way_L2_old = jsx.dwUpos;
                    }
                    else if (joyGetPos(i, ref js) == 0)   // 查詢指定的遊戲桿設備的位置和活動性
                    {
                        info.JoyEx = false;
                        info.Way_X_old = js.wXpos;
                        info.Way_Y_old = js.wYpos;
                        info.Way_Z_old = js.wZpos;
                        info.Way_L2_old = jsx.dwUpos;
                    }
                    else continue; //裝置功能失效

                    //set button count
                    info.ButtonCount = joycap.wNumButtons;

                    info.Button_old = 0;

                    if (joycap.wPeriodMin > PeriodMin)
                        PeriodMin = joycap.wPeriodMin;

                    joyinfo_list.Add(info);
                }
            }
            //取出所有目前連線遊戲手把中最慢的PeriodMin然後+5ms
            PeriodMin += 95;
            new Thread(polling_listener).Start();
            st.Stop();
            //Console.WriteLine("init joypad infor : " + st.ElapsedMilliseconds + " ms");
        }
        int button_type = 0;

        //string start_value = "\xFA";
        //char start_value = Convert.ToChar();
        //char start_value = Encoding.UTF8.GetString(Encoding.GetEncoding("UTF16").GetChars("\xFF"));
        //char start_value = '\xFF';
        
        /*string option = "\x00";
        string value = "\xFF";
        string left_st = "\xFF";
        string left_nd = "\xFF";
        string front_pn = "\xFF";
        string right_st = "\xFF";
        string right_nd = "\xFF";
        string back_pn = "\xFF";
        string end_value = "\x0B";
        string end_value_2 = "\x0A";
        string end_value_3 = "\x0D";*/
        string data;
        string client_data;
        int V = 0;

        byte[] start_value = { 0xff };
        byte[] option = { 0x00 };
        byte[] value = { 0xff };
        byte[] left_st = { 0xff };
        byte[] left_nd = { 0xff };
        byte[] front_pn = { 0xff };
        byte[] right_st = { 0xff };
        byte[] back_pn = { 0xff };
        byte[] end_value = { 0x0b };
        byte[] end_value_2 = { 0x0a };
        byte[] end_value_3 = { 0x0d };
        byte[] sendbyte = { 0xfa, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0b };

        int release = 0;

        private void Form1_Load(object sender, EventArgs e)
        {
            sendbyte[0] = 0xfa;
            sendbyte[1] = 0x10;
            sendbyte[2] = 0x00;
            sendbyte[3] = 0x00;
            sendbyte[4] = 0x00;
            sendbyte[5] = 0x00;
            sendbyte[6] = 0x10;
            sendbyte[7] = 0x10;
            sendbyte[8] = 0x10;
            ZLoc.Text = sendbyte[8].ToString();
            YLoc.Text = sendbyte[0].ToString() + " " + sendbyte[1].ToString() + " " + sendbyte[2].ToString() + " " + sendbyte[3].ToString() +
                        " " + sendbyte[4].ToString() + " " + sendbyte[5].ToString() + " " + sendbyte[6].ToString() + " " + sendbyte[7].ToString() + " " + sendbyte[8].ToString();
        }

        List<joystickEvent> joy_event_captur()
        {
            List<joystickEvent> event_list = new List<joystickEvent>();
            for (int i_button = 0; i_button < joyinfo_list.Count(); i_button++)
            {
                DeviceJoyInfo button_inf = joyinfo_list[i_button];
                int button_id = button_inf.ID;
                int button_count = button_inf.ButtonCount;

                int button_now, X_now, Y_now, Z_now;
                int L2_now;
                if (button_inf.JoyEx == false)  
                {
                    joyGetPos(button_id, ref js);
                    button_now = js.wButtons;
                    button_type = 1;
                    X_now = js.wXpos;
                    Y_now = js.wYpos;
                    Z_now = js.wZpos;
                    L2_now = jsx.dwUpos;
                }
                else  
                {
                    joyGetPosEx(button_id, ref jsx);
                    button_now = jsx.dwButtons;
                    button_type = 2;
                    X_now = jsx.dwXpos;
                    Y_now = jsx.dwYpos;
                    Z_now = jsx.dwZpos;
                    L2_now = jsx.dwUpos;
                }

                int button_old = button_inf.Button_old;
                int X_old = button_inf.Way_X_old;
                int Y_old = button_inf.Way_Y_old;
                int Z_old = button_inf.Way_Z_old;
                int R_old = button_inf.Way_R_old;
                int L2_old = button_inf.Way_L2_old;

                button_inf.Button_old = button_now;
                button_inf.Way_X_old = X_now;
                button_inf.Way_Y_old = Y_now;
                button_inf.Way_Z_old = Z_now;
                button_inf.Way_L2_old = L2_now;

                joyinfo_list[i_button] = button_inf;
                if (button_old != button_now || button_now != 0)
                {
                    for (int i = 0; i < button_count; i++)
                    {
                        if ((button_now & 1) != 0)  //按下按鈕
                        {
                            joystickEvent event_item = new joystickEvent();
                            event_item.event_type = 1;    //1：判定為按鈕
                            event_item.joystick_id = button_inf.ID;
                            event_item.button_id = i + 1;
                            event_item.button_event = 1;   //1：判定為壓下
                            event_list.Add(event_item);
                        }
                        else
                        {
                            if ((button_now & 1) != (button_old & 1))  //鬆開按鈕
                            {
                                joystickEvent event_item = new joystickEvent();
                                event_item.event_type = 1;
                                event_item.joystick_id = button_inf.ID;
                                event_item.button_id = i + 1;
                                event_item.button_event = 0;   //0：判定為鬆開
                                event_list.Add(event_item);
                            }
                        }
                        button_now >>= 1;
                        button_old >>= 1;
                    }
                }
                
                if (X_old != X_now)
                {
                     joystickEvent event_item = new joystickEvent();
                     event_item.event_type = 0;    //0：判定為方向鍵
                     event_item.joystick_id = button_inf.ID;
                     event_item.way_type = 0;      //0：判定為x方向
                     event_item.way_value = X_now;
                     event_list.Add(event_item);
                }

                if (Y_old != Y_now)
                {
                    joystickEvent event_item = new joystickEvent();
                    event_item.event_type = 0;
                    event_item.joystick_id = button_inf.ID;
                    event_item.way_type = 1;    //1：判定為y方向
                    event_item.way_value = Y_now;
                    event_list.Add(event_item);
                }

                if (Z_old != Z_now)
                {
                    joystickEvent event_item = new joystickEvent();
                    event_item.event_type = 0;
                    event_item.joystick_id = button_inf.ID;
                    event_item.way_type = 2;    //2：判定為z方向
                    event_item.way_value = Z_now;
                    event_list.Add(event_item);
                }

                if (L2_old != L2_now)
                {
                    joystickEvent event_item = new joystickEvent();
                    event_item.event_type = 0;
                    event_item.joystick_id = button_inf.ID;
                    event_item.way_type = 3;    //2：判定為l2方向
                    event_item.way_value = L2_now;
                    event_list.Add(event_item);
                }
            }
            return event_list;
        }

        bool app_running = true;
        
        public unsafe void text(int joy_id, int joy_value, int X, int Y, int Z, int R)
        {
            label3.Text = "button_type:" + button_type + "原始值  X值：" + X + "  " + "Y值：" + Y + "  " + "Z值：" + Z + "  " + "V值：" + V;
        }
        
        void polling_listener()
        {
            int X = 0, Y = 0 ,Z = 0 ,R = 0 ;
            
            while (app_running)
            {
                Thread.Sleep(PeriodMin); //在指定時間（毫秒）暫停執行緒
                List<joystickEvent> event_list = joy_event_captur();
                
                foreach (joystickEvent joy_event in event_list)  //迴圈
                {
                    
                    double angle = 0;
                    if (joy_event.event_type == 0) //方向鍵觸發
                    {
                        if (joy_event.way_type == 0) //x  右
                        {
                            /*if (joy_event.way_value > 34935)      //1.增加左右值的上下限 左：0 右：65535 (中：32767,32000-33000)  2.有速率 
                            {
                                pic.Left += (joy_event.way_value - 34935) / 120;

                                start_value = 250.ToString("X2");
                                option = 4.ToString("X2");
                                value = ((joy_event.way_value - 34935) / 120).ToString("X2");

                                left_st = 255.ToString("X2");
                                left_nd = 255.ToString("X2");
                                front_pn = 0.ToString("X2");      //0x
                                right_st = 255.ToString("X2");
                                right_nd = 255.ToString("X2");
                                back_pn = 0.ToString("X2");      //0x

                                end_value = 251.ToString("X2");
                                end_value_2 = 13.ToString("X2");
                                end_value_3 = 10.ToString("X2");

                                /*data = NA_2 + start_value + NA_2 + option + NA_2 + value + NA_2 + left_st + NA_2 + left_nd + NA_2 + front_pn + NA_2 +
                                right_st + NA_2 + right_nd + NA_2 + back_pn + NA_2 + end_value + NA_2 + end_value_2 + NA_2 + end_value_3;
                            }
                            else if (joy_event.way_value < 30600)    //左
                            {
                                pic.Left -= (30600 - joy_event.way_value) / 120;

                                start_value = 250.ToString("X2");
                                option = 3.ToString("X2");
                                value = ((30600 - joy_event.way_value) / 120).ToString("X2");
                                
                                left_st = 255.ToString("X2");
                                left_nd = 255.ToString("X2");
                                front_pn = 0.ToString("X2");     //0x
                                right_st = 255.ToString("X2");
                                right_nd = 255.ToString("X2");
                                back_pn = 0.ToString("X2");      //0x

                                end_value = 251.ToString("X2");
                                end_value_2 = 13.ToString("X2");
                                end_value_3 = 10.ToString("X2");

                                /*data = NA_2 + start_value + NA_2 + option + NA_2 + value + NA_2 + left_st + NA_2 + left_nd + NA_2 + front_pn + NA_2 +
                                right_st + NA_2 + right_nd + NA_2 + back_pn + NA_2 + end_value + NA_2 + end_value_2 + NA_2 + end_value_3;
                            } */                           
                            X = joy_event.way_value;
                        }
                        else if(joy_event.way_type == 1)//y
                        {
                            /*if (joy_event.way_value > 34935)      // 後退    1.增加左右值的上下限 左：0 右：65535 (中：32767,32000-33000)  2.有速率 
                            {
                                pic.Top += (joy_event.way_value - 34935) / 120;
                         
                                start_value = 250.ToString("X2");
                                option = 2.ToString("X2");
                                value = ((joy_event.way_value - 34935) / 120).ToString("X2");

                                left_st = 255.ToString("X2");
                                left_nd = 255.ToString("X2");
                                front_pn = 0.ToString("X2");     //0x
                                right_st = 255.ToString("X2");
                                right_nd = 255.ToString("X2");
                                back_pn = 0.ToString("X2");     //0x

                                end_value = 251.ToString("X2");
                                end_value_2 = 13.ToString("X2");
                                end_value_3 = 10.ToString("X2");

                                /*data = NA_2 + start_value + NA_2 + option + NA_2 + value + NA_2 + left_st + NA_2 + left_nd + NA_2 + front_pn + NA_2 +
                                right_st + NA_2 + right_nd + NA_2 + back_pn + NA_2 + end_value + NA_2 + end_value_2 + NA_2 + end_value_3;
                            }
                            else if (joy_event.way_value < 30600)   //前進
                            {
                                pic.Top -= (30600 - joy_event.way_value) / 120;

                                start_value = 250.ToString("X2");
                                option = 1.ToString("X2");
                                value = ((30600 - joy_event.way_value) / 120).ToString("X2");

                                left_st = 255.ToString("X2");
                                left_nd = 255.ToString("X2");
                                front_pn = 0.ToString("X2");    //0x
                                right_st = 255.ToString("X2");
                                right_nd = 255.ToString("X2");
                                back_pn = 0.ToString("X2");     //0x

                                end_value = 251.ToString("X2");
                                end_value_2 = 13.ToString("X2");
                                end_value_3 = 10.ToString("X2");

                                /*data = NA_2 + start_value + NA_2 + option + NA_2 + value + NA_2 + left_st + NA_2 + left_nd + NA_2 + front_pn + NA_2 +
                                right_st + NA_2 + right_nd + NA_2 + back_pn + NA_2 + end_value + NA_2 + end_value_2 + NA_2 + end_value_3;
                            }*/

                            Y = joy_event.way_value;
                        }
                        if (X > 30600 && X < 34935 && Y < 34935 && Y > 30600 && release != 1)   //放開
                        {
                            /*option = "\x00";
                            value = "\x00";

                            left_st = "\x00";
                            left_nd = "\x00";
                            front_pn = "\x00";
                            right_st = "\x00";
                            right_nd = "\x00";
                            back_pn = "\x00";*/
                            
                            byte[] option = { 0x00 };
                            byte[] value = { 0x00 };
                            byte[] left_st = { 0x00 };
                            byte[] left_nd = { 0x00 };
                            front_pn[0] = 0x00;
                            byte[] right_st = { 0x00 };
                            byte[] back_pn = { 0x00 };
                            sendbyte[0] = 0xfa;
                            sendbyte[1] = 0x00;
                            sendbyte[2] = 0x00;
                            sendbyte[3] = 0x00;
                            sendbyte[4] = 0x00;
                            sendbyte[1] = front_pn[0];
                            sendbyte[1] = 0x00;
                            //byte[] sendbyte = { 0xfa, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0b, 0x0a, 0x0d };
                        }
                        else if (X >= 49000 && X <= 63000 && Y >= 2000 && Y <= 9500)   //  45°
                        {
                            /*option = "\x07";
                            value = "\x00";

                            left_st = "\xFF";
                            left_nd = "\x00";
                            front_pn = "\x00";   //0X00
                            right_st = "\x00";
                            right_nd = "\xFF";
                            back_pn = "\x00";    //0X00

                            /*start_value = 250.ToString("X2");
                            option = 7.ToString("X2");
                            value = 50.ToString("X2");

                            left_st = 255.ToString("X2");
                            left_nd = 0.ToString("X2");
                            front_pn = 0.ToString("X2");  //0x00
                            right_st = 0.ToString("X2");
                            right_nd = 255.ToString("X2");
                            back_pn = 0.ToString("X2");  //0x00

                            end_value = 251.ToString("X2");
                            end_value_2 = 13.ToString("X2");
                            end_value_3 = 10.ToString("X2");*/
                            byte[] sendbyte = { 0xfa, 0x07, 0xff, 0x00, 0x00, 0x00, 0xff, 0x00, 0x0b };
                        }
                        else if (X >= 3000 && X <= 20000 && Y >= 3000 && Y <= 20000)   //  135°
                        {
                            /*option = "\x07";
                            value = "\x00";

                            left_st = "\xFF";
                            left_nd = "\x00";
                            front_pn = "\x00";   //0X00
                            right_st = "\x00";
                            right_nd = "\xFF";
                            back_pn = "\x00";    //0X00

                            /*start_value = 250.ToString("X2");
                            option = 7.ToString("X2");
                            value = 50.ToString("X2");

                            left_st = 255.ToString("X2");
                            left_nd = 0.ToString("X2");
                            front_pn = 0.ToString("X2");  //0x00
                            right_st = 0.ToString("X2");
                            right_nd = 255.ToString("X2");
                            back_pn = 0.ToString("X2");  //0x00

                            end_value = 251.ToString("X2");
                            end_value_2 = 13.ToString("X2");
                            end_value_3 = 10.ToString("X2");*/
                            byte[] sendbyte = { 0xfa, 0x07, 0xff, 0x00, 0x00, 0x00, 0xff, 0x00, 0x0b };
                        }
                        else if (X >= 2000 && X <= 16000 && Y >= 46000 && Y <= 60000)   //  225°
                        {
                            /*option = "\x07";
                            value = "\x00";

                            left_st = "\xFF";
                            left_nd = "\x00";
                            front_pn = "\x01";   //0X01
                            right_st = "x00";
                            right_nd = "\xFF";
                            back_pn = "\x10";    //0X10

                            /*start_value = 250.ToString("X2");
                            option = 7.ToString("X2");
                            value = 50.ToString("X2");

                            left_st = 255.ToString("X2");
                            left_nd = 0.ToString("X2");
                            front_pn = 1.ToString("X2");  //0x01
                            right_st = 0.ToString("X2");
                            right_nd = 255.ToString("X2");
                            back_pn = 16.ToString("X2");  //0x10

                            end_value = 251.ToString("X2");
                            end_value_2 = 13.ToString("X2");
                            end_value_3 = 10.ToString("X2");*/
                            byte[] sendbyte = { 0xfa, 0x07, 0x00, 0xff, 0x00, 0x01, 0xff, 0x10, 0x0b };
                        }
                        else if (X >= 48000 && X <= 60000 && Y >= 57000 && Y <= 65000)   //  315°
                        {
                            /*option = "\x07";
                            value = "\x00";

                            left_st = "\xFF";
                            left_nd = "\x00";
                            front_pn = "\x10";   //0X10
                            right_st = "\x00";
                            right_nd = "\xFF";
                            back_pn = "\x01";    //0X01

                            /*start_value = 250.ToString("X2");
                            option = 7.ToString("X2");
                            value = 50.ToString("X2");

                            left_st = 255.ToString("X2");
                            left_nd = 0.ToString("X2");
                            front_pn = 16.ToString("X2");  //0x10
                            right_st = 0.ToString("X2");
                            right_nd = 255.ToString("X2");
                            back_pn = 1.ToString("X2");  //0x01

                            end_value = 251.ToString("X2");
                            end_value_2 = 13.ToString("X2");
                            end_value_3 = 10.ToString("X2");*/
                            byte[] sendbyte = { 0xfa, 0x07, 0xff, 0x00, 0x10, 0x00, 0xff, 0x01, 0x0b };
                        }
                        else if (X > 34935 && Y >= 24000 && Y <= 44000)      // 右   1.增加左右值的上下限 左：0 右：65535 (中：32767,32000-33000)  2.有速率 
                        {
                            byte[] sendbyte = { 0xfa, 0x04, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x0b };
                            /*option = "\x04";
                            value = "\xFF";

                            left_st = "\xFF";
                            left_nd = "\xFF";
                            front_pn = "\xFF";   //0X01
                            right_st = "\xFF";
                            right_nd = "\xFF";
                            back_pn = "\xFF";    //0X10

                            /*V = ((X - 34935) / 3000);
                            switch (V)
                            {
                                case 1:
                                    value = "\x19";
                                    break;
                                case 2:
                                    value = "\x32";
                                    break;
                                case 3:
                                    value = "\x4B";
                                    break;
                                case 4:
                                    value = "\x64";
                                    break;
                                case 5:
                                    value = "\x7D";
                                    break;
                                case 6:
                                    value = "\x96";
                                    break;
                                case 7:
                                    value = "\xAF";
                                    break;
                                case 8:
                                    value = "\xC8";
                                    break;
                                case 9:
                                    value = "\xE1";
                                    break;
                                case 10:
                                    value = "\xFF";
                                    break;
                            }*/


                            /*start_value = 250.ToString("X2");
                            option = 4.ToString("X2");
                            value = ((X - 34935) / 120).ToString("X2");

                            left_st = 255.ToString("X2");
                            left_nd = 255.ToString("X2");
                            front_pn = 255.ToString("X2");      //0x01
                            right_st = 255.ToString("X2");
                            right_nd = 255.ToString("X2");
                            back_pn = 255.ToString("X2");      //0x10

                            end_value = 251.ToString("X2");
                            end_value_2 = 13.ToString("X2");
                            end_value_3 = 10.ToString("X2");*/
                        }
                        else if (X < 30600 && Y >= 24000 && Y <= 44000)    //左
                        {
                            byte[] sendbyte = { 0xfa, 0x03, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x0b };
                            /*option = "\x03";
                            value = "\xFF";

                            left_st = "\xFF";
                            left_nd = "\xFF";
                            front_pn = "\xFF";   //0X10
                            right_st = "\xFF";
                            right_nd = "\xFF";
                            back_pn = "\xFF";    //0X01


                            V = ((30600 - X) / 3000);
                            /*start_value = 250.ToString("X2");
                            option = 3.ToString("X2");
                            value = ((30600 - X) / 120).ToString("X2");

                            left_st = 255.ToString("X2");
                            left_nd = 255.ToString("X2");
                            front_pn = 255.ToString("X2");     //0x10
                            right_st = 255.ToString("X2");
                            right_nd = 255.ToString("X2");
                            back_pn = 255.ToString("X2");      //0x01

                            end_value = 251.ToString("X2");
                            end_value_2 = 13.ToString("X2");
                            end_value_3 = 10.ToString("X2");*/
                        }
                        else if (Y > 34935 && X >= 25000 && X <= 43000)      // 後退    1.增加左右值的上下限 左：0 右：65535 (中：32767,32000-33000)  2.有速率 
                        {
                            byte[] sendbyte = { 0xfa, 0x02, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x0b };
                            /*option = "\x02";
                            value = "\xFF";
                            //value = "\x" + ((Y - 34935) / 120).ToString("X2");

                            left_st = "\xFF";
                            left_nd = "\xFF";
                            front_pn = "\xFF";   //0X11
                            right_st = "\xFF";
                            right_nd = "\xFF";
                            back_pn = "\xFF";    //0X11

                            V = ((Y - 34935) / 3000);
                            /*start_value = 250.ToString("X2");
                            option = 2.ToString("X2");
                            value = ((Y - 34935) / 120).ToString("X2");

                            left_st = 255.ToString("X2");
                            left_nd = 255.ToString("X2");
                            front_pn = 255.ToString("X2");     //0x11
                            right_st = 255.ToString("X2");
                            right_nd = 255.ToString("X2");
                            back_pn = 255.ToString("X2");     //0x11

                            end_value = 251.ToString("X2");
                            end_value_2 = 13.ToString("X2");
                            end_value_3 = 10.ToString("X2");*/
                        }
                        else if (Y < 30600 && X >= 27000 && X <= 36000)   //前進
                        {
                            byte[] sendbyte = { 0xfa, 0x01, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x0b };
                            /*option = "\x01";
                            value = "\xFF";

                            left_st = "\xFF";
                            left_nd = "\xFF";
                            front_pn = "\xFF";   //0X00
                            right_st = "\xFF";
                            right_nd = "\xFF";
                            back_pn = "\xFF";    //0X00


                            V = ((30600 - Y) / 3000);
                           
                            /*start_value = 250.ToString("X2");
                            option = 1.ToString("X2");
                            value = ((30600 - Y) / 120).ToString("X2");

                            left_st = 255.ToString("X2");
                            left_nd = 255.ToString("X2");
                            front_pn = 255.ToString("X2");    //0x00
                            right_st = 255.ToString("X2");
                            right_nd = 255.ToString("X2");
                            back_pn = 255.ToString("X2");     //0x00

                            end_value = 251.ToString("X2");
                            end_value_2 = 13.ToString("X2");
                            end_value_3 = 10.ToString("X2");*/
                        }
                        

                        text(joy_event.button_id, joy_event.way_value, X, Y, Z, R);                        
                    }
                    else if(joy_event.event_type == 1) //一般按鈕觸發
                    {
                        if (joy_event.button_event == 0)    //放開
                        {
                            release = 0;
                            byte[] sendbyte = { 0xfa, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0b };
                            //Console.WriteLine("裝置 " + joy_event.joystick_id + " ,按鈕 " + joy_event.button_id + " 放開");

                            /*option = "\x00";
                            value = "\x00";

                            left_st = "\xFF";
                            left_nd = "\xFF";
                            front_pn = "\xFF";  
                            right_st = "\xFF";
                            right_nd = "\xFF";
                            back_pn = "\xFF";   

                            /*start_value = 250.ToString("X2");
                            option = 0.ToString("X2");
                            value = 0.ToString("X2");

                            left_st = 0.ToString("X2");
                            left_nd = 0.ToString("X2");
                            front_pn = 0.ToString("X2");   //0x00
                            right_st = 0.ToString("X2");
                            right_nd = 0.ToString("X2");
                            back_pn = 0.ToString("X2");    //0x00

                            end_value = 251.ToString("X2");
                            end_value_2 = 13.ToString("X2");
                            end_value_3 = 10.ToString("X2");*/
                        }
                        else
                        {
                            //Console.WriteLine("裝置 " + joy_event.joystick_id + " ,按鈕 " + joy_event.button_id + " 壓下");   //前進後退
                            if (joy_event.button_id == 8)     //右自轉        1.前進後退沒有速率 2.如果可以改變增加上下限 3.將上下限改為256個階段
                            {
                                //YLoc.Text = "L2:" + joy_event.way_value;
                                release = 1;
                                byte[] sendbyte = { 0xfa, 0x06, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x0b };
                                /*option = "\x06";
                                value = "\xFF";

                                left_st = "\xFF";
                                left_nd = "\xFF";
                                front_pn = "\xFF";  
                                right_st = "\xFF";
                                right_nd = "\xFF";
                                back_pn = "\xFF";    

                                /*start_value = 250.ToString("X2");
                                option = 6.ToString("X2");
                                value = 50.ToString("X2");

                                left_st = 255.ToString("X2");
                                left_nd = 255.ToString("X2"); 
                                front_pn = 255.ToString("X2");    //0x00
                                right_st = 255.ToString("X2"); 
                                right_nd = 255.ToString("X2");
                                back_pn = 255.ToString("X2");   //0x11

                                end_value = 251.ToString("X2");
                                end_value_2 = 13.ToString("X2");
                                end_value_3 = 10.ToString("X2");*/
                                //Console.WriteLine("裝置 " + joy_event.joystick_id);
                            }
                            else if (joy_event.button_id == 7)   //左自轉
                            {
                                release = 1;
                                byte[] sendbyte = { 0xfa, 0x05, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x0b };
                                /*option = "\x05";
                                value = "\xFF";

                                left_st = "\xFF";
                                left_nd = "\xFF";
                                front_pn = "\xFF";
                                right_st = "\xFF";
                                right_nd = "\xFF";
                                back_pn = "\xFF";

                                /*start_value = 250.ToString("X2");
                                option = 5.ToString("X2");
                                value = 50.ToString("X2");
                                
                                left_st = 255.ToString("X2");
                                left_nd = 255.ToString("X2");
                                front_pn = 255.ToString("X2");    //0x11
                                right_st = 255.ToString("X2");
                                right_nd = 255.ToString("X2");
                                back_pn = 0.ToString("X2");     //0x00

                                end_value = 251.ToString("X2");
                                end_value_2 = 13.ToString("X2");
                                end_value_3 = 10.ToString("X2");*/
                            }

                            
                            /*data = start_value + option + value + left_st + left_nd + front_pn + right_st +
                                right_nd + back_pn + end_value + end_value_2 + end_value_3;*/

                            /*data = start_value[0] + option[0] + value[0] + left_st[0] + left_nd[0] + front_pn[0] + right_st[0] + 
                                right_nd[0] + back_pn[0] + end_value[0] + end_value_2[0] + end_value_3[0] + "";*/
                            /*data = NA_2[0] + start_value + NA_2 + option + NA_2 + value + NA_2 + left_st + NA_2 + left_nd + NA_2 + front_pn + NA_2 +
                                right_st + NA_2 + right_nd + NA_2 + back_pn + NA_2 + end_value + NA_2 + end_value_2 + NA_2 + end_value_3;*/
                        }
                    }
                    
                    //byte[] sendbyte = { start_value,option };
                    data = start_value + "";
                    client_data = data;
                    ZLoc.Text = client_data;
                    YLoc.Text = sendbyte[0].ToString() + " " + sendbyte[1].ToString() + " " + sendbyte[2].ToString() + " " + sendbyte[3].ToString() +
                        " " + sendbyte[4].ToString() + " " + sendbyte[5].ToString() + " " + sendbyte[6].ToString() + " " + sendbyte[7].ToString() + " " + sendbyte[8].ToString();
                    Send(sendbyte);
                }
            }
        }
              
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            app_running = false;
        }

        private void InitialeCompnent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.X_loc = new System.Windows.Forms.Label();
            this.Y_loc = new System.Windows.Forms.Label();
            this.pic = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pic)).BeginInit();
            this.SuspendLayout();
            // 
            // X_loc
            // 
            this.X_loc.AllowDrop = true;
            this.X_loc.AutoSize = true;
            this.X_loc.Location = new System.Drawing.Point(437, 359);
            this.X_loc.Name = "X_loc";
            this.X_loc.Size = new System.Drawing.Size(41, 15);
            this.X_loc.TabIndex = 0;
            this.X_loc.Text = "label1";
            // 
            // Y_loc
            // 
            this.Y_loc.AutoSize = true;
            this.Y_loc.Location = new System.Drawing.Point(437, 393);
            this.Y_loc.Name = "Y_loc";
            this.Y_loc.Size = new System.Drawing.Size(41, 15);
            this.Y_loc.TabIndex = 1;
            this.Y_loc.Text = "label2";
            // 
            // pic
            // 
            this.pic.Image = ((System.Drawing.Image)(resources.GetObject("pic.Image")));
            this.pic.Location = new System.Drawing.Point(505, 206);
            this.pic.Name = "pic";
            this.pic.Size = new System.Drawing.Size(100, 50);
            this.pic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pic.TabIndex = 2;
            this.pic.TabStop = false;
            // 
            // Form1
            // 
            this.ClientSize = new System.Drawing.Size(1140, 627);
            this.Controls.Add(this.pic);
            this.Controls.Add(this.Y_loc);
            this.Controls.Add(this.X_loc);
            this.Name = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.pic)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void Send(byte[] str)
        {
            var address = "tcp://10.22.25.50:55688";
            //var address = "tcp://127.0.0.1:55688";
            byte[] data_1 = null;
            /*if(str == null)
            {
                ZLoc.Text = "00";
            }
            else
            {
                ZLoc.Text = Encoding.ASCII.GetString(str).ToString();
            }*/
            using (var client = new RequestSocket(address))
            {               
                var time = TimeSpan.FromMilliseconds(5000);
                /*if (str == null)
                {
                    ZLoc.Text = "00";

                    option = "\x00";
                    value = "\x00";

                    left_st = "\xFF";
                    left_nd = "\xFF";
                    front_pn = "\xFF";
                    right_st = "\xFF";
                    right_nd = "\xFF";
                    back_pn = "\xFF";

                    str = start_value + option + value + left_st + left_nd + front_pn + right_st +
                                right_nd + back_pn + end_value + end_value_2 + end_value_3;
                }*/

                //client.TrySendFrame(time,data_2);
                //client.TrySendMultipartBytes(time, data);
                //ZLoc.Text = Encoding.ASCII.GetString(data_1).ToString();
                //data_1 = Encoding.UTF32.GetBytes(str);
                client.TrySendFrame(time, str);
             

                var timeout = TimeSpan.FromSeconds(5000);
                client.TryReceiveFrameString(timeout, out string result);

                Debug.WriteLine(result);
                label4.Text = result;
                client.Close();
            }
        }

        private void send_Click(object sender, EventArgs e)
        {
            //Send(client_data);
        }
    }
}
