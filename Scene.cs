using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CompanionStart
{
    public class Scene : FrameworkElement
    {
        public double T;
        public bool Export;
        public bool Muted;
        public bool StartupMonitored;
        public bool TargetReady;
        public bool Waiting;
        public string StartupError;
        public double ActivityTime;
        public int RenderCount;
        static readonly Color Green = Color.FromRgb(107, 242, 177);
        static readonly Color CodeGreen = Color.FromRgb(106, 207, 157);
        static readonly Color Teal = Color.FromRgb(86, 217, 197);
        static readonly Color White = Color.FromRgb(235, 247, 243);
        static readonly Color Dim = Color.FromRgb(102, 151, 139);
        static readonly Color Warm = Color.FromRgb(241, 217, 164);
        static readonly Color Black = Color.FromRgb(3, 12, 15);
        static readonly Typeface Mono = new Typeface("Consolas");
        static readonly Typeface UI = new Typeface("Microsoft YaHei UI");
        static readonly ImageSource Logo = LoadLogo();
        static readonly Point[] Nodes = MakeNodes();
        static readonly string[] Code = MakeCode();
        const double ModelStart = 7.56;
        const double ModelEnd = 11.85;
        static readonly ModelStage[] Models = {
            new ModelStage(7.56, "轻度", "Codex-3"),
            new ModelStage(7.91, "轻度", "GPT-5.5"),
            new ModelStage(8.26, "中高", "GPT-5.6 Luna"),
            new ModelStage(8.91, "中高", "GPT-5.6 Terra"),
            new ModelStage(9.26, "极高", "GPT-5.6 Sol"),
            new ModelStage(9.91, "极高", "GPT-5.6 Turbo"),
            new ModelStage(10.26, "极高", "GPT-6 Luna"),
            new ModelStage(10.61, "极高", "GPT-6 Sol"),
            new ModelStage(11.41, "Ultra", "GPT-6 Astra")
        };
        static readonly Popup[] Windows = {
            new Popup(58, 217, 442, 141, 6.55, Teal, "ACCESS / GATEWAY", "Conversation channel", "A space for every thought", "LISTEN FIRST // EXCHANGE OPEN", "CHANNEL READY"),
            new Popup(730, 141, 805, 249, 7.10, Color.FromRgb(137,198,238), "CONTEXT / INDEX", "Ideas find their place", "From scattered thoughts to a clear path", "UNDERSTAND // ORGANIZE // CONNECT", "STRUCTURE READY"),
            new Popup(653, 432, 363, 130, 7.65, Color.FromRgb(176,160,225), "TOOLS / RUNTIME", "A small next step", "One step. Then the next.", "", "READY"),
            new Popup(87, 562, 789, 249, 8.15, Color.FromRgb(226,173,136), "THOUGHT / ROUTING", "Direction takes shape", "Understand the question. Find a way forward.", "IDEA → CLARITY → ACTION", "PATH ALIGNED"),
            new Popup(883, 465, 519, 162, 8.65, Warm, "ENGINE / SIGNAL", "A steady signal", "Patience. Clarity. Momentum.", "", "CORE ONLINE"),
            new Popup(426, 784, 520, 164, 9.15, Color.FromRgb(132,189,241), "SURFACE / COMPOSITION", "Interface assembled", "Your idea is where we begin", "", "COMPOSITION COMPLETE"),
            new Popup(1211, 683, 577, 137, 9.55, Teal, "REFLECTION / FOCUS", "Think with care", "Stay curious. Keep moving.", "", "FOCUS READY")
        };

        static ImageSource LoadLogo()
        {
            if (!File.Exists(Path.Combine(Program.Root, "OpenAI-logo.png")))
            {
                // The public package ships an original vector fallback; OpenAI artwork is read locally at install time.
                var drawing = new DrawingGroup();
                using (var d = drawing.Open())
                {
                    d.DrawRectangle(Brushes.Transparent, null, new Rect(0,0,256,256));
                    d.PushTransform(new ScaleTransform(.88,.88,128,128));
                    var pen = new Pen(Brushes.White, 8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    d.DrawGeometry(null, pen, Geometry.Parse("M 56,190 C 16,190 10,132 41,119 C 37,70 80,50 111,70 C 140,46 183,71 185,111 C 229,125 229,188 188,190 Z"));
                    d.DrawGeometry(null, pen, Geometry.Parse("M 81,116 L 109,143 L 81,168 M 132,168 L 165,168"));
                    d.Pop();
                }
                drawing.Freeze(); var fallback = new DrawingImage(drawing); fallback.Freeze(); return fallback;
            }
            var im = new BitmapImage(); im.BeginInit(); im.CacheOption = BitmapCacheOption.OnLoad;
            im.UriSource = new Uri(Path.Combine(Program.Root, "OpenAI-logo.png")); im.EndInit(); im.Freeze(); return im;
        }
        static Point[] MakeNodes()
        {
            var random = new Random(142); var points = new Point[34];
            for (int i=0;i<points.Length;i++) points[i] = new Point(28+random.NextDouble()*344, 470+random.NextDouble()*374);
            return points;
        }
        static string[] MakeCode()
        {
            string[] motifs = {"the first thought", "a quiet constellation", "a clear direction", "one small next step", "a patient conversation", "room for every question", "a little more clarity"};
            string[] variables = {"firstSignal", "thoughtMap", "clearPath", "nextStep", "openChannel", "quietRoom", "afterglow"};
            var lines = new System.Collections.Generic.List<string>();
            for (int k=0;k<motifs.Length;k++)
            {
                lines.Add("// " + motifs[k] + " / listen for the shape of an idea");
                lines.Add("const " + variables[k] + " = await companion.compose({");
                lines.Add("  intent: \"understand before moving forward\",");
                lines.Add("  signal: \"a thought finds its first connection\",");
                lines.Add("  palette: [\"#6CF5C8\", \"#89EFC0\", \"warm white\"],");
                lines.Add("  rhythm: \"steady, curious, and unhurried\",");
                lines.Add("  listen: idea => leaveRoomFor(idea),");
                lines.Add("  focus: \"make the important things clearer\",");
                lines.Add("  layers: [\"question\", \"context\", \"possibility\"],");
                lines.Add("  connect: fragments => findMeaning(fragments),");
                lines.Add("  action: \"one useful step, then another\",");
                lines.Add("  onReady: path => beginTogether(path)");
                lines.Add("}); // a little space for whatever comes next");
                lines.Add(""); lines.Add("");
                lines.Add("// with patient hands / give the thought a direction");
                lines.Add("function moveForward(question) {");
                lines.Add("  const facts = understand(question);");
                lines.Add("  const path = clarify(facts, possibilities);");
                lines.Add("  return build({ path, care: true, together: true });");
                lines.Add("}"); lines.Add(""); lines.Add("");
            }
            return lines.ToArray();
        }
        static double Clamp(double x) {return Math.Max(0,Math.Min(1,x));}
        static double Ease(double x) {x=Clamp(x);return x*x*(3-2*x);}
        static double In(double t,double start,double length) {return Ease((t-start)/length);}
        static Color Mix(Color first,Color second,double p)
        {
            p=Clamp(p);
            return Color.FromRgb((byte)(first.R+(second.R-first.R)*p),(byte)(first.G+(second.G-first.G)*p),(byte)(first.B+(second.B-first.B)*p));
        }
        static double CompilationProgress(double t) {return Clamp((t-5.0)/(ModelEnd-5.0));}
        static double LogoAngle(double t)
        {
            // Counterclockwise cubic acceleration, fitted to the early reference frames.
            double elapsed=Math.Max(0,t-.2);
            return -(5.6*elapsed+36.7*elapsed*elapsed+29.3*elapsed*elapsed*elapsed);
        }
        static double LogoSpeed(double t)
        {
            double elapsed=Math.Max(0,t-.2);
            return 5.6+73.4*elapsed+87.9*elapsed*elapsed;
        }
        static SolidColorBrush B(Color c,double a) {var b=new SolidColorBrush(Color.FromArgb((byte)(Clamp(a)*255),c.R,c.G,c.B));b.Freeze();return b;}
        static Pen P(Color c,double a,double w) {var p=new Pen(B(c,a),w);p.StartLineCap=PenLineCap.Round;p.EndLineCap=PenLineCap.Round;p.Freeze();return p;}
        static void Text(DrawingContext d,string s,double x,double y,double size,Color c,double a,bool ui=false,bool center=false)
        {
            if (a<=.001 || s.Length==0) return;
            var ft=new FormattedText(s,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,ui?UI:Mono,size,B(c,a),1.0);
            d.DrawText(ft,new Point(center?x-ft.Width/2:x,y));
        }
        static void Line(DrawingContext d,double x1,double y1,double x2,double y2,Color c,double a,double w=1)
        {d.DrawLine(P(c,a,w),new Point(x1,y1),new Point(x2,y2));}
        static void Glow(DrawingContext d,double x,double y,double r,double a,Color c)
        {
            var brush=new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(B(c,a).Color,0));
            brush.GradientStops.Add(new GradientStop(B(c,a*.14).Color,.5));
            brush.GradientStops.Add(new GradientStop(B(c,0).Color,1));
            d.DrawEllipse(brush,null,new Point(x,y),r,r);
        }
        static void Arc(DrawingContext d,double cx,double cy,double r,double start,double sweep,Color c,double a,double w)
        {
            if(a<=.001 || sweep<=0)return;
            double s=start*Math.PI/180,e=(start+Math.Min(359.8,sweep))*Math.PI/180;
            var geometry=new StreamGeometry();
            using(var g=geometry.Open())
            {g.BeginFigure(new Point(cx+r*Math.Cos(s),cy+r*Math.Sin(s)),false,false);g.ArcTo(new Point(cx+r*Math.Cos(e),cy+r*Math.Sin(e)),new Size(r,r),0,sweep>180,SweepDirection.Clockwise,true,false);}
            d.DrawGeometry(null,P(c,a,w),geometry);
        }

        protected override void OnRender(DrawingContext d)
        {
            base.OnRender(d);RenderCount++;
            double scale=Math.Min(ActualWidth/1920,ActualHeight/1080);if(scale<=0)return;
            d.PushTransform(new TranslateTransform((ActualWidth-1920*scale)/2,(ActualHeight-1080*scale)/2));
            d.PushTransform(new ScaleTransform(scale,scale));
            double t=T, fade=Waiting?1:1-In(t,14.65,.35), desktop=1-In(t,4.55,.7), ui=In(t,4.65,.55);
            if(Export)
            {
                d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(14,28,39),Color.FromRgb(6,12,20),30),null,new Rect(0,0,1920,1080));
                Glow(d,1140,425,730,.17,Color.FromRgb(64,148,181));
                Glow(d,190,180,850,.05,Warm);
            }
            d.PushOpacity(fade);
            d.DrawRectangle(B(Black,ui),null,new Rect(-1000,-1000,3920,3080));
            if(desktop>.001) DrawDesktopLogo(d,t,desktop);
            if(ui>.001)
            {
                d.PushOpacity(ui);
                DrawTexture(d,t);
                DrawSidebar(d,t);
                DrawCenter(d,t);
                DrawCompiler(d,t);
                foreach(var w in Windows) DrawPopup(d,t,w);
                DrawReady(d,t);
                double progress=CompilationProgress(t);
                Line(d,0,1076,1920,1076,Teal,.16,4);
                Line(d,0,1076,1920*progress,1076,Teal,.85,3.5);
                Glow(d,1920*progress,1076,26,.27,Teal);
                d.Pop();
            }
            d.Pop();d.Pop();d.Pop();
        }

        static void DrawDesktopLogo(DrawingContext d,double t,double a)
        {
            double appear=In(t,.02,.18),illuminate=In(t,.20,1.1),size=450*(.97+.03*In(t,.4,3.0));
            var rect=new Rect(960-size/2,537-size/2,size,size);
            d.PushOpacity(a*appear);
            double motion=Clamp((LogoSpeed(t)-800)/1000);
            if(motion>.001)
            {
                DrawSpinningMark(d,rect,t-.008,illuminate,.035*motion);
                DrawSpinningMark(d,rect,t-.004,illuminate,.055*motion);
            }
            DrawSpinningMark(d,rect,t,illuminate,1);
            // Segments remain in fixed positions and accumulate clockwise, as in the reference.
            const int segments=36;
            double progress=Clamp((t-.05)/3.6);
            int loaded=(int)Math.Floor(progress*segments+1e-6);
            for(int i=0;i<loaded;i++)
            {
                double strength=i==loaded-1 && loaded<segments?.92:.76;
                Arc(d,960,537,376,-90+i*10,5.4,Teal,strength,4.0);
            }
            d.Pop();
        }
        static void DrawSpinningMark(DrawingContext d,Rect rect,double t,double illuminate,double opacity)
        {
            var mask=new ImageBrush(Logo){Stretch=Stretch.Fill,RelativeTransform=new RotateTransform(LogoAngle(t),.5,.5)};
            d.PushOpacity(opacity);
            d.PushOpacityMask(mask);
            d.DrawRectangle(new LinearGradientBrush(Mix(Color.FromRgb(3,43,52),Color.FromRgb(0,180,162),illuminate),Mix(Color.FromRgb(2,31,40),Color.FromRgb(0,201,118),illuminate),35),null,rect);
            d.Pop();d.Pop();
        }

        static void DrawTexture(DrawingContext d,double t)
        {
            for(int col=0;col<6;col++)
                for(int row=0;row<40;row++)
                {
                    double y=(row*31+t*(9+col*2))%1100-15;
                    Text(d,(row+col)%3==0?"01":"·",442+col*123,y,9,Teal,.022+((row+col)%5)*.003);
                }
            for(int i=0;i<50;i++) d.DrawEllipse(B(Teal,.065),null,new Point(15+(i*151)%1090,(i*71+t*11)%1060),.7,.7);
            Line(d,405,0,405,1080,Teal,.035);
            Line(d,1105,0,1105,1080,Teal,.025);
        }

        static void NavButton(DrawingContext d,string label,double y,double a)
        {
            d.DrawRoundedRectangle(B(Color.FromRgb(24,56,51),a*.82),P(Teal,a*.16,1),new Rect(13,y,377,35),9,9);
            Text(d,label,23,y+9,13,Green,a*.82,true);
        }
        static void DrawSidebar(DrawingContext d,double t)
        {
            double a=In(t,5,.45);
            d.DrawRectangle(B(Color.FromRgb(9,24,26),a),null,new Rect(0,0,403,1080));
            Glow(d,30,400,400,.025*a,Green);
            Text(d,"Codex",22,23,23,Green,a,true);
            NavButton(d,"+  New chat",80,a);
            NavButton(d,"Search conversations",124,a*In(t,5.25,.3));
            NavButton(d,"Library",168,a*In(t,5.45,.3));
            string[] secondary={"Explore GPTs","Recent conversations","Project workspace","Settings"};
            for(int i=0;i<secondary.Length;i++)NavButton(d,secondary[i],211+i*44,a*In(t,6.35+i*.18,.4));
            double graph=In(t,6.25,1.25);
            Text(d,"NEURAL CONSTELLATION",24,408,10,CodeGreen,graph*.7);
            Text(d,"VERTICAL SIGNAL MAP",24,425,9,Dim,graph*.7);
            Line(d,23,441,378,441,Teal,graph*.08);
            for(int i=0;i<68;i++)
            {
                int j=i%34,k=(i*7+11)%34;Point n=Nodes[j],p=Nodes[k];
                if(Math.Abs(n.X-p.X)+Math.Abs(n.Y-p.Y)<295)Line(d,n.X,n.Y,p.X,p.Y,Teal,graph*.07);
            }
            for(int i=0;i<Nodes.Length;i++)
            {
                Point n=Nodes[i];double pulse=.6+.4*Math.Sin(t*1.1+i*.82);
                Glow(d,n.X,n.Y,15,graph*.11*pulse,Teal);
                d.DrawEllipse(null,P(Teal,graph*.3,1),n,4.5,4.5);
                d.DrawEllipse(B(Green,graph*(.3+.4*pulse)),null,n,1.6,1.6);
            }
            for(int i=0;i<48;i++)d.DrawEllipse(B(White,graph*.2),null,new Point(21+(i*131)%354,468+(i*179)%369),.9,.9);
            Text(d,"RUNTIME SIGNAL",25,895,9,Dim,graph*.85);
            Text(d,"NODES 34   LINKS 68",192,895,9,Dim,graph*.85);
            for(int i=0;i<29;i++)
            {
                double h=13+38*(.5+.5*Math.Sin(i*.48-t*1.8));
                d.DrawRectangle(B(Teal,graph*(.23+.08*Math.Sin(i))),null,new Rect(26+i*12.1,962-h,10,h));
            }
            Line(d,25,963,377,963,Teal,graph*.15);
            Text(d,t<ModelEnd?"SIGNAL FLOW // SYNCHRONIZING":"SIGNAL FLOW // ONLINE",24,1042,9,CodeGreen,graph*.8);
        }

        static void SmallMark(DrawingContext d,double x,double y,double a)
        {
            d.PushTransform(new TranslateTransform(x,y));
            var shape=Geometry.Parse("M -27,18 C -47,18 -48,-9 -32,-14 C -27,-33 -8,-34 3,-25 C 19,-30 35,-20 35,-7 C 52,0 43,21 27,21 L -27,21");
            d.DrawGeometry(null,P(Green,a*.07,7),shape);d.DrawGeometry(null,P(Green,a*.95,2.4),shape);
            Line(d,-15,-7,-6,1,Green,a,2);Line(d,-6,1,-15,9,Green,a,2);
            Line(d,1,9,15,9,Warm,a*.95,2.3);d.Pop();
        }
        static void DrawCenter(DrawingContext d,double t)
        {
            Text(d,"Codex",425,23,19,Green,In(t,5.8,.5),true);
            double appear=In(t,6.2,1.0);
            SmallMark(d,754,330,appear);
            Text(d,"我们要构建什么？",755,425,31,Green,appear,true,true);
            string[] labels={"Create","Explore","Think"};
            for(int i=0;i<3;i++)
            {
                double a=In(t,7.65+i*.25,.7);double x=421+i*229;
                d.DrawRoundedRectangle(B(Color.FromRgb(20,53,47),a*.88),P(Teal,a*.09,1),new Rect(x,756+8*(1-a),218,57),12,12);
                Text(d,labels[i],x+14,778+8*(1-a),14,Green,a*.85,true);
            }
            double input=In(t,6.95,.65);
            d.DrawRoundedRectangle(B(Color.FromRgb(32,62,58),input),P(Teal,input*.08,1),new Rect(413,880,690,181),20,20);
            Text(d,"Message Codex ...",431,894,14,White,input*.78,true);
            DrawModelLoader(d,t);
        }

        class ModelStage
        {
            public double Start; public string Level,Name;
            public ModelStage(double start,string level,string name) {Start=start;Level=level;Name=name;}
        }
        static void DrawModelLoader(DrawingContext d,double t)
        {
            double select=In(t,ModelStart-.1,.35);
            if(select<=.001)return;
            double p=.05+.95*Clamp((t-ModelStart)/(ModelEnd-ModelStart));
            double online=In(t,ModelEnd-.18,.18);
            Color neon=Color.FromRgb(35,255,130),edge=Mix(Teal,neon,online);
            var selector=new Rect(837,969,259,85);
            Glow(d,966,1013,159,select*(.035+.075*online),edge);
            d.DrawRoundedRectangle(B(Black,select),P(edge,select*(.5+.25*online),1.35),selector,15,15);
            d.DrawRoundedRectangle(null,P(edge,select*online*.07,5),selector,15,15);
            var lightning=Geometry.Parse("M 858,978 L 848,990 L 856,990 L 851,1004 L 866,986 L 858,986 Z");
            d.DrawGeometry(B(neon,select*online*.96),P(Mix(White,neon,online),select*.8,1.7),lightning);
            d.DrawRoundedRectangle(B(Color.FromRgb(5,55,45),select*(.5+.30*online)),null,new Rect(891,973,177,49),10,10);
            int index=0;
            for(int i=1;i<Models.Length;i++)if(t>=Models[i].Start)index=i;
            double transition=In(t,Models[index].Start,.10);
            if(index>0 && transition<1)
            {
                Text(d,Models[index-1].Level,972,977-3*transition,12.5,Green,select*(1-transition),true,true);
                Text(d,Models[index-1].Name,972,995-3*transition,14,White,select*(1-transition),true,true);
            }
            Text(d,Models[index].Level,972,977+3*(1-transition),12.5,Green,select*transition,true,true);
            Text(d,Models[index].Name,972,995+3*(1-transition),14,White,select*transition,true,true);
            Text(d,"›",1055,995,15,Green,select*.7,true);
            var track=new Rect(850,1028,232,12);
            d.DrawRoundedRectangle(B(Color.FromRgb(22,68,53),select*.85),null,track,6,6);
            double filledWidth=Math.Min(232,16+216*p);
            var fill=new Rect(850,1028,filledWidth,12);
            var gradient=new LinearGradientBrush(Mix(Color.FromRgb(84,213,175),Color.FromRgb(14,254,110),online),Mix(Color.FromRgb(119,242,198),Color.FromRgb(128,255,178),online),0);
            gradient.Opacity=select*.95;
            d.DrawRoundedRectangle(gradient,null,fill,6,6);
            d.PushClip(new RectangleGeometry(fill,6,6));
            for(int i=0;i<58;i++)Line(d,851+i*4,1030,851+i*4,1038,White,select*(.05+.06*online),.6);
            Line(d,850,1031,1082,1031,White,select*(.12+.12*online),.7);
            Line(d,850,1036,1082,1036,Green,select*.19,.7);
            foreach(double marker in new double[]{.08,.52,.94})
                d.DrawEllipse(B(White,select*.3),null,new Point(850+232*marker,1034),1.8,1.8);
            d.Pop();
            double knobX=858+216*p;
            Glow(d,knobX,1034,20,select*(.2+.18*online),neon);
            d.DrawEllipse(null,P(Mix(Teal,neon,online),select*.6,1.3),new Point(knobX,1034),10,10);
            d.DrawEllipse(B(Mix(White,Color.FromRgb(221,255,231),online),select*.98),null,new Point(knobX,1034),8,8);
        }

        static void DrawCompiler(DrawingContext d,double t)
        {
            double a=In(t,5.15,.4);
            Text(d,"CODEX ENGINE / INTERFACE COMPILER",1132,18,13,CodeGreen,a*.9);
            d.PushClip(new RectangleGeometry(new Rect(1127,64,783,955)));
            int shown=Math.Max(0,(int)((t-5.25)*13.3));
            int scroll=Math.Max(0,(int)((t-8.9)*6.1));
            double offset=((t-8.9)*6.1-scroll)*19;
            if(t<8.9)offset=0;
            for(int i=0;i<53;i++)
            {
                int index=i+scroll;if(index>=Code.Length||index>shown)continue;
                string line=Code[index];
                if(index==shown)line=line.Substring(0,Math.Min(line.Length,(int)Math.Max(0,((t-5.25)*13.3-shown)*Math.Max(24,line.Length))));
                double y=67+i*19-offset;
                double edge=y<80?Clamp((y-64)/16):1;
                Color c=line.StartsWith("//")?CodeGreen:Green;
                Text(d,line,1132,y,13.4,c,a*(line.StartsWith("//")?.66:.73)*edge);
            }
            d.Pop();
            int progress=(int)(CompilationProgress(t)*100);
            Text(d,t<ModelEnd?"COMPILING  "+progress+"%":"CORE ONLINE / INTERFACE READY",1132,1036,13,CodeGreen,a*.92);
        }

        class Popup
        {
            public double X,Y,W,H,Start;public Color Color;public string Header,Title,Description,Micro,Status;
            public Popup(double x,double y,double w,double h,double start,Color c,string header,string title,string description,string micro,string status)
            {X=x;Y=y;W=w;H=h;Start=start;Color=c;Header=header;Title=title;Description=description;Micro=micro;Status=status;}
        }
        static void DrawPopup(DrawingContext d,double t,Popup p)
        {
            double opening=In(t,p.Start,.38),closing=1-In(t,10.7+(p.Start-6.55)*.18,.55),a=opening*closing;
            if(a<=.001)return;
            double x=p.X,y=p.Y+16*(1-opening)-9*(1-closing);
            d.DrawRoundedRectangle(B(Color.FromRgb(0,4,8),a*.25),null,new Rect(x+5,y+8,p.W,p.H),9,9);
            d.DrawRoundedRectangle(B(Color.FromRgb(4,13,19),a*.97),P(p.Color,a*.56,1),new Rect(x,y,p.W,p.H),9,9);
            Line(d,x+9,y+1,x+p.W-9,y+1,p.Color,a*.8,1.6);
            Line(d,x+12,y+29,x+p.W-12,y+29,p.Color,a*.13);
            d.DrawEllipse(B(p.Color,a*.85),null,new Point(x+14,y+14),3,3);
            Text(d,p.Header,x+23,y+9,9,p.Color,a*.87);
            Text(d,"×",x+p.W-23,y+7,15,Dim,a*.8,true);
            double heading=p.H>200?74:47;
            Text(d,p.Title,x+13,y+heading,16,White,a*.95,true);
            Text(d,p.Description,x+13,y+heading+32,11,Dim,a*.95);
            if(p.Micro.Length>0 && p.H>200)Text(d,p.Micro,x+13,y+p.H-84,9,CodeGreen,a*.8);
            double pillWidth=Math.Max(116,Math.Min(p.W-30,p.Status.Length*6.2+28));
            d.DrawRoundedRectangle(B(Color.FromRgb(4,45,40),a*.68),null,new Rect(x+13,y+p.H-30,pillWidth,19),3,3);
            d.DrawRectangle(B(p.Color,a*.9),null,new Rect(x+19,y+p.H-23,5,5));
            Text(d,p.Status,x+30,y+p.H-26,9,White,a*.83);
        }

        void DrawReady(DrawingContext d,double t)
        {
            double a=In(t,12.0,.45);if(a<=.001)return;
            double x=555,y=457+12*(1-a),w=810,h=165;
            d.DrawRoundedRectangle(B(Black,a*.98),P(Teal,a*.73,1.4),new Rect(x,y,w,h),12,12);
            Line(d,x+20,y+12,x+w-20,y+12,Teal,a*.84,1.6);
            bool failed=StartupError!=null;
            bool pending=StartupMonitored && !TargetReady;
            string title=failed?"CODEX COULD NOT START":pending?"CODEX IS STARTING":"SYSTEM READY TO LAUNCH";
            Text(d,title,960,y+33,failed||pending?43:48,White,a,false,true);
            Text(d,"AI改变世界",960,y+103,14,CodeGreen,a,true,true);
            if(failed)
            {
                Text(d,"重新尝试  ›",960,y+137,13,White,a*.91,true,true);
                Text(d,"未能打开 Codex，请检查安装后重试。",960,y+187,14,Dim,a,true,true);
            }
            else if(pending)
            {
                string dots=new string('·',1+(int)(ActivityTime*2)%3);
                Text(d,(Waiting?"正在等待 Codex 准备就绪":"正在同步启动 Codex")+"  "+dots,960,y+137,13,CodeGreen,a*.95,true,true);
                if(Waiting && ActivityTime>60) Text(d,"启动时间较长，仍在等待窗口响应。",960,y+187,13,Dim,a,true,true);
            }
            else Text(d,"› ENTER  ↵",960,y+137,13,White,a*.91,false,true);
        }
        public bool IsSkipPoint(Point point)
        {
            double s=Math.Min(ActualWidth/1920,ActualHeight/1080);
            if(s<=0)return false;
            double x=(point.X-(ActualWidth-1920*s)/2)/s,y=(point.Y-(ActualHeight-1080*s)/2)/s;
            return T>=12 && x>=555 && x<=1365 && y>=457 && y<=634;
        }
    }
}
