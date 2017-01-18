using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slois
{
	public class Pins
	{
		[Serializable()]
		public class Pin
		{
			public int num { get; set; }
			public bool output { get; set; } //true - output, false - input
			public bool on { get; set; }
		}

		List<Pin> pins;

		public Pins()
		{
			pins = new List<Pin>();
		}

		public List<Pin> Content
		{
			get { return pins; }
		}

		public void Save(string fileName)
		{
			System.IO.StreamWriter sw = new System.IO.StreamWriter(fileName);
			foreach (Pin p in pins)
			{
				sw.WriteLine(p.num.ToString() + ';' + (p.output ? '1' : '0') + ';' + (p.on ? '1' : '0'));
			}
			sw.Close();
		}

		public void Load(string fileName)
		{
			string[] ss = System.IO.File.ReadAllLines(fileName);
			pins.Clear();
			for (int i = 0; i < ss.Length; i++)
			{
				string[] sss = ss[i].Split(';');
				if (sss.Length == 3)
				{
					int num = 0, output = 0, on = 0;
					int.TryParse(sss[0], out num);
					int.TryParse(sss[1], out output);
					int.TryParse(sss[2], out on);
					Pin p = new Pin();
					p.num = num;
					p.output = output == 1 ? true : false;
					p.on = on == 1 ? true : false;
					pins.Add(p);
				}
			}
		}
	}
}
