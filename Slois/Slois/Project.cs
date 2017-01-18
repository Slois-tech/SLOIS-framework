using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;

namespace Slois
{
	public class Project
	{
		public class Servo
		{
			public int pin; //номер пина сервомотора
			public float exactly; //точность обучения
			public bool used;

			internal void Save(System.IO.StreamWriter sw)
			{
				sw.WriteLine(pin.ToString() + ';' + exactly.ToString(System.Globalization.CultureInfo.InvariantCulture) + ';' + (used ? 1 : 0));
			}

			internal void Load(System.IO.StreamReader sr)
			{
				string s = sr.ReadLine();
				string[] ss = s.Split(';');
				if (ss.Length == 3)
				{
					int.TryParse(ss[0], out pin);
					float.TryParse(ss[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out exactly);
					int used2;
					int.TryParse(ss[2], out used2);
					used = used2 == 1 ? true : false;
				}
			}
		}

		public class Frame
		{
			public string name { get; set; }
			public System.Drawing.Image image;
			public float[] outputs;

			public Frame()
			{
			}

			public Frame(string name, System.Drawing.Image image, float[] outputs)
			{
				this.name = name;
				this.image = image;
				this.outputs = new float[5];
				for (int i = 0; i < outputs.Length && i < 5; i++)
				{
					this.outputs[i] = outputs[i];
				}
			}

			internal bool Load(System.IO.StreamReader sr)
			{
				string s = sr.ReadLine();
				string[] ss = s.Split(';');
				if (ss.Length == 7)
				{
					name = ss[0];
					outputs = new float[5];
					for (int i = 0; i < 5; i++)
						outputs[i] = float.Parse(ss[1 + i], System.Globalization.CultureInfo.InvariantCulture);
					byte[] mem = Convert.FromBase64String(ss[6]);
					using (System.IO.MemoryStream ms = new System.IO.MemoryStream(mem))
					{
						image = System.Drawing.Image.FromStream(ms);
					}
					return true;
				}
				return false;
			}

			internal void Save(System.IO.StreamWriter sw)
			{
				sw.Write(name); sw.Write(';');
				for (int i = 0; i < outputs.Length; i++)
				{
					sw.Write(outputs[i]); sw.Write(';');
				}
				using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
				{
					image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
					sw.WriteLine(Convert.ToBase64String(ms.ToArray()));
				}
			}

			internal static float[] ImageToFloat(Image image)
			{
				Bitmap bmp = (Bitmap)image;
				BitmapData m = bmp.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
				float[] img_gray = new float[m.Width * m.Height]; //здесь будет серое изображение
				unsafe
				{
					fixed (float* p = img_gray)
					{
						float* p1 = p;
						for (int h = 0; h < m.Height; h++) //идем по строкам
						{
							byte* p2 = ((byte*)m.Scan0) + h * m.Stride;
							for (int w = 0; w < m.Width; w++)
							{
								//*p1 = (byte)(.299 * p2[0] + .587 * p2[1] + .114 * p2[2]); //0 - red, 1 - green, 2 - blue
								*p1 = (float)((.2125 * p2[0] + .7154 * p2[1] + .0721 * p2[2]) / 255.0); //0 - red, 1 - green, 2 - blue
								p1++;
								p2 += 3;
							}
						}
					}
				}
				bmp.UnlockBits(m);
				return img_gray;
			}

			internal float[] GetInputs()
			{
				return ImageToFloat(image);
			}
		}

		string name;
		List<Servo> servos;
		List<Frame> frames;
		List<NeuroNet> nets;

		public Project()
		{
			servos = new List<Servo>();
			frames = new List<Frame>();
			nets = new List<NeuroNet>();
		}

		public string Name
		{
			get { return name; }
			set { name = value; }
		}

		public List<Servo> Servos
		{
			get { return servos; }
		}

		public List<Frame> Frames
		{
			get { return frames; }
		}

		public void ClearServos()
		{
			servos.Clear();
		}

		public void AddServo(int pinServo, float exactly, bool used)
		{
			Servo s = new Servo();
			s.pin = pinServo;
			s.exactly = exactly;
			s.used = used;
			servos.Add(s);
		}

		public void AddFrame(string name, System.Drawing.Image image, float[] outputs)
		{
			frames.Add(new Frame(name, image, outputs));
		}

		public void DelFrame(Frame f)
		{
			frames.Remove(f);
		}

		public void Load(string fileName)
		{
			System.IO.StreamReader sr = new System.IO.StreamReader(fileName);
			name = sr.ReadLine();
			string count_s = sr.ReadLine();
			int count = int.Parse(count_s);
			servos.Clear();
			for (int i = 0; i < count; i++)
			{
				Servo s = new Servo();
				s.Load(sr);
				servos.Add(s);
			}

			count_s = sr.ReadLine();
			count = int.Parse(count_s);
			frames.Clear();
			for (int i = 0; i < count; i++)
			{
				Frame f = new Frame();
				if (f.Load(sr))
					frames.Add(f);
			}

			count_s = sr.ReadLine();
			count = int.Parse(count_s);
			nets.Clear();
			for (int i = 0; i < count; i++)
			{
				NeuroNet nn = new NeuroNet();
				nn.Load(sr);
				nets.Add(nn);
			}

			sr.Close();
		}

		public void Save(string fileName)
		{
			System.IO.StreamWriter sw = new System.IO.StreamWriter(fileName);
			sw.WriteLine(name);

			sw.WriteLine(servos.Count);
			foreach (Servo s in servos)
				s.Save(sw);

			sw.WriteLine(frames.Count.ToString());
			foreach (Frame f in frames)
				f.Save(sw);

			sw.WriteLine(nets.Count.ToString());
			foreach(NeuroNet nn in nets)
			{
				if (nn == null)
					sw.WriteLine(0);
				else
					nn.Save(sw);
			}

			sw.Close();
		}

		public float Train(NeuroNet.FuncStat fs, int maxEpochs)
		{
			float[][] inputs = new float[frames.Count][];
			for (int i = 0; i < frames.Count; i++)
			{
				inputs[i] = frames[i].GetInputs();
			}
			nets.Clear();
			NeuroNet.Stat st = new NeuroNet.Stat();
			st.exact = 0;
			st.epoch = 0;
			int countOut = 0;
			for (int i = 0; i < servos.Count; i++)
			{
				if (servos[i].used)
				{
					NeuroNet net = new NeuroNet();
					for (int f = 0; f < frames.Count; f++)
					{
						net.AddExample(inputs[f], frames[f].outputs[i]);
					}
					int epoch;
					st.exact += net.Train(servos[i].exactly, maxEpochs, fs, out epoch);
					countOut++;
					if (st.epoch < epoch)
						st.epoch = epoch;
					nets.Add(net);
				}
				else
					nets.Add(null);
			}
			st.state = 1;
			st.exact /= countOut;
			fs(st);
			return st.exact;
		}

		public float[] GetOutputs(Image image)
		{
			float[] inputs = Frame.ImageToFloat(image);
			float[] outputs = new float[servos.Count];
			for (int i = 0; i < servos.Count; i++)
			{
				if (i < nets.Count && nets[i] != null)
				{
					outputs[i] = nets[i].Execute(inputs);
				}
			}
			return outputs;
		}

		public class Short
		{
			public string name;
			public string comment;
			public string fileName;

			internal Short()
			{
			}

			internal Short(string name, string fileName)
			{
				this.name = name;
				comment = "";
				this.fileName = fileName;
			}

			internal void Save(System.IO.StreamWriter sw)
			{
				sw.WriteLine(name);
				sw.WriteLine('-');
				sw.WriteLine(comment);
				sw.WriteLine('-');
				sw.WriteLine(fileName);
			}

			internal bool Load(System.IO.StreamReader sr)
			{
				name = sr.ReadLine();
				if (name == null) return false;
				string s = sr.ReadLine();
				if (s == null) return false;
				comment = "";
				if (s == "-")
				{
					while (true)
					{
						s = sr.ReadLine();
						if (s == null) return false;
						if (s == "-") break;
						if (comment.Length > 0) comment += "\r\n";
						comment += s;
					}
				}
				fileName = sr.ReadLine();
				if (fileName == null) return false;
				return true;
			}

			public string Name
			{
				get { return name; }
			}

			static public List<Short> projects = new List<Short>();

			static public void Save(string fileName)
			{
				System.IO.StreamWriter sw = new System.IO.StreamWriter(fileName);
				foreach (Short p in projects)
					p.Save(sw);
				sw.Close();
			}

			static public void Load(string fileName)
			{
				System.IO.StreamReader sr = new System.IO.StreamReader(fileName);
				projects.Clear();
				while (true)
				{
					Short p = new Short();
					try
					{
						if (!p.Load(sr)) break;
					}
					catch
					{
						break;
					}
					projects.Add(p);
				}
				sr.Close();
			}

			static public void Add(string name, string fileName)
			{
				int i = projects.FindIndex(p => p.name == name);
				if( i < 0 )
				{
					Short p = new Short(name, fileName);
					projects.Add(p);
				}
			}

			static public void Del(string name)
			{
				int i = projects.FindIndex(p => p.name == name);
				if (i >= 0)
				{
					projects.RemoveAt(i);
				}
			}
		}
	}
}
