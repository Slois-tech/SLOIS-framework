using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using AForge.Video.DirectShow;
using System.IO.Ports;

namespace Slois
{
	public partial class FormMain : Form
	{
		delegate void DelegatePrintStat(NeuroNet.Stat st);
		delegate void DelegateSetOutputs(float[] outputs);

		CheckBox[] buttons;
		Pins pins;
		BindingList<Pins.Pin> b_pins;
		ComboBox[] cbServos;
		TextBox[] tbOpers;
		TextBox[] tbOuts;
		TextBox[] tbExacts;
		CheckBox[] checkOns;
		Project project;
		string fileNameProject;
		VideoCaptureDevice videoCapture;
		DelegatePrintStat funcPrintStat;
		DelegateSetOutputs funcSetOutputs;
		DateTime timeBegTrain;
		bool sendedToServo = false;
		Thread threadSendSignal;
		int mouseX, mouseY;
		int manualControl = 0; //1 - режим А, 2 - режим Б
		float[] manualPosServos; //значения в сервах для ручного управления
		const int minValueServo = 544;
		const int maxValueServo = 2400;
		bool stopTrain;

		class PutSignalServo
		{
			Project project;
			float[] outputs;
			int numCom;

			internal PutSignalServo(Project project, float[] outputs, int numCom)
			{
				this.project = project;
				this.outputs = outputs;
				this.numCom = numCom;
			}

			internal void Execute()
			{
				SerialPort com = new SerialPort();
				int speed = 57600;
				com.PortName = "COM" + numCom.ToString();
				com.ReadTimeout = 1000;
				com.WriteTimeout = 1000;
				com.BaudRate = speed;
				//System.Diagnostics.Debug.WriteLine(outputs[0].ToString());
				int pin = -1;
				byte[] cmd = new byte[3];
				try
				{
					com.Open();
					int n = 0;
					foreach (Project.Servo s in project.Servos)
					{
						if (s.used)
						{
							pin = s.pin;
							cmd[0] = (byte)(n + 1);
							cmd[1] = (byte)pin;
							byte angle = 0;
							if (outputs[n] < 544)
								angle = 0;
							else if (outputs[n] > 2400)
								angle = 180;
							else
								angle = (byte)((outputs[n] - 544) * 180 / (2400 - 544));
							cmd[2] = angle;
							com.Write(cmd, 0, 3);
						}
						n++;
					}
					com.Close();
				}
				catch (Exception e)
				{
					//MessageBox.Show("Ошибка при отправки данных на Ардуину (pin " + pin.ToString() + "). " + e.ToString());
				}
			}
		}

		public FormMain()
		{
			InitializeComponent();
			buttons = new CheckBox[] { btCameras, btPins, btOperation, btProjects };

			Dictionary<bool, string> io = new Dictionary<bool, string>();
			io[true] = "OUTPUT";
			io[false] = "INPUT";
			clnIO.DataSource = new BindingSource(io, null);
			clnIO.ValueMember = "Key";
			clnIO.DisplayMember = "Value";

			pins = new Pins();

			cbServos = new ComboBox[] { cbServo1, cbServo2, cbServo3, cbServo4, cbServo5 };
			tbOpers = new TextBox[] { tbOper1, tbOper2, tbOper3, tbOper4, tbOper5 };
			tbOuts = new TextBox[] { tbOut1, tbOut2, tbOut3, tbOut4, tbOut5 };
			tbExacts = new TextBox[] { tbExact1, tbExact2, tbExact3, tbExact4, tbExact5 };
			checkOns = new CheckBox[] { checkOn1, checkOn2, checkOn3, checkOn4, checkOn5 };

			fileNameProject = "";
			funcPrintStat = PrintStat;
			funcSetOutputs = SetOutputs;

			udSensitivityMouse.Value = 5;
			udMoveMouse.Value = 10;
			udSensitivityWheel.Value = 10;

			this.MouseWheel += FormMain_MouseMove;
		}

		void SetProject(Project prj)
		{
			project = prj;
			tbNameProject.Text = prj.Name;
			int n = 0;
			foreach (var s in prj.Servos)
			{
				cbServos[n].SelectedIndex = ((List<int>)cbServos[n].DataSource).FindIndex(i => i == s.pin);
				tbOuts[n].Text = "";
				tbExacts[n].Text = s.exactly.ToString(System.Globalization.CultureInfo.InvariantCulture);
				checkOns[n].Checked = s.used;
				n++;
				if (n >= cbServos.Length) break;
			}
			for (int i = n; i < cbServos.Length; i++)
			{
				cbServos[i].SelectedIndex = -1;
				tbOuts[i].Text = "";
				tbExacts[i].Text = "";
				checkOns[i].Checked = false;
			}
			UpdateListFrames();
		}

		void LoadPins(string fileName)
		{
			if (!System.IO.File.Exists(fileName)) return;
			pins.Load(fileName);
			b_pins = new BindingList<Pins.Pin>(pins.Content);
			gridPins.DataSource = b_pins;

			List<int> numPins = new List<int>();
			foreach (var p in pins.Content)
			{
				if (p.on && p.output)
					numPins.Add(p.num);
			}
			for (int i = 0; i < cbServos.Length; i++)
			{
				cbServos[i].DataSource = numPins.ToList();
			}

		}

		void UpdateProject()
		{
			project.ClearServos();
			project.Name = tbNameProject.Text;
			for (int i = 0; i < cbServos.Length; i++)
			{
				float exact;
				
				float.TryParse(tbExacts[i].Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out exact);
				int pinServo = cbServos[i].SelectedValue == null ? -1 : (int)cbServos[i].SelectedValue;
				project.AddServo(pinServo, exact, checkOns[i].Checked);
			}
			UpdateListFrames();
		}

		void UpdateListFrames()
		{
			cbImages.DataSource = null;
			cbImages.DataSource = project.Frames;
			cbImages.DisplayMember = "name";
			tbCountImages.Text = cbImages.Items.Count.ToString();
		}

		float[] UpdateOutputs(Image img)
		{
			if (project != null && img != null)
			{
				float[] outputs = project.GetOutputs(img);
				return outputs;
			}
			return null;
		}

		void LoadShortProjects(string fileName)
		{
			if (!System.IO.File.Exists(fileName)) return;
			Project.Short.Load(fileName);
			UpdatePageProjects();
		}

		void UpdatePageProjects()
		{
			lbProjects.DataSource = null;
			lbProjects.DataSource = Project.Short.projects;
			lbProjects.DisplayMember = "Name";

		}

		void InitListCameras()
		{
			cbCameras.Items.Clear();
			FilterInfoCollection cams = new FilterInfoCollection(FilterCategory.VideoInputDevice);
			cbCameras.DisplayMember = "Name";
			cbCameras.ValueMember = "MonikerString";
			cbCameras.DataSource = cams;
		}

		bool CallbackStatTrain(NeuroNet.Stat st)
		{
			Invoke(funcPrintStat, new object[]{st});
			return stopTrain;
		}

		void PrintStat(NeuroNet.Stat st)
		{
			if ((st.epoch % 10) == 0 || st.state == 1)
			{
				tbEpochs.Text = st.epoch.ToString();
				tbNetExact.Text = st.exact.ToString();
				TimeSpan diff = DateTime.Now.Subtract(timeBegTrain);
				tbTimeTrain.Text = diff.TotalSeconds.ToString();
			}
			if (st.state == 1)
			{
				MessageBox.Show("Обучение окончено");
			}
		}

		void Train()
		{
			stopTrain = false;
			timeBegTrain = DateTime.Now;
			project.Train(CallbackStatTrain, 1000000);
		}

		void SendSignalToServos(float[] outputs)
		{
			if (threadSendSignal == null || threadSendSignal.ThreadState == ThreadState.Stopped)
			{
				PutSignalServo pss = new PutSignalServo(project, outputs, (int)udCOM.Value);
				threadSendSignal = new Thread(pss.Execute);
				threadSendSignal.Start();
			}
		}

		void OnManualRejim()
		{
			if (manualPosServos == null) manualPosServos = new float[cbServos.Length];
			if (chRejimA.Checked)
			{
				for (int i = 0; i < manualPosServos.Length; i++)
					manualPosServos[i] = minValueServo;
				manualControl = 1;
			}
			else if (chRejimB.Checked)
			{
				for (int i = 0; i < manualPosServos.Length; i++)
				{
					if (!float.TryParse(tbOpers[i].Text, out manualPosServos[i]))
						manualPosServos[i] = minValueServo;
				}
				manualControl = 2;
			}
			else
			{
				manualControl = 0;
			}
			if (manualControl != 0)
			{
				mouseX = Cursor.Position.X;
				mouseY = Cursor.Position.Y;
				SendSignalToServos(manualPosServos);
				this.Capture = true;
			}
		}

		void SetOutputs(float[] outputs)
		{
			for (int i = 0; i < outputs.Length; i++)
				tbOuts[i].Text = ((int)(outputs[i] + 0.999)).ToString();
		}

		private void btCameras_Click(object sender, EventArgs e)
		{
			for (int i = 0; i < buttons.Length; i++)
			{
				if (buttons[i] == sender)
				{
					buttons[i].Checked = true;
					tabControl.SelectedIndex = i;
				}
				else
				{
					buttons[i].Checked = false;
				}
			}
		}

		private void gridPins_DataError(object sender, DataGridViewDataErrorEventArgs e)
		{
		}

		private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
		{
			pins.Save(System.IO.Path.Combine( Application.StartupPath, "pins.txt"));
			Project.Short.Save(System.IO.Path.Combine(Application.StartupPath, "projects.txt"));
			if (videoCapture != null)
			{
				videoCapture.Stop();
			}
		}

		private void FormMain_Load(object sender, EventArgs e)
		{
			LoadPins(System.IO.Path.Combine(Application.StartupPath, "pins.txt"));
			SetProject(new Project());
			LoadShortProjects(System.IO.Path.Combine(Application.StartupPath, "projects.txt"));
			InitListCameras();
		}

		private void btSaveProject_Click(object sender, EventArgs e)
		{
			UpdateProject();
			if (fileNameProject.Length == 0)
			{
				dlgSaveFile.Filter = "Projects|*.prj";
				if (dlgSaveFile.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					fileNameProject = dlgSaveFile.FileName;
				else
					return;
			}
			project.Save(fileNameProject);
			Project.Short.Add(project.Name, fileNameProject);
			UpdatePageProjects();
		}

		private void btLoad_Click(object sender, EventArgs e)
		{
			if (lbProjects.SelectedIndex >= 0)
			{
				Project.Short p = lbProjects.SelectedValue as Project.Short;
				Project prj = new Project();
				prj.Load(p.fileName);
				fileNameProject = p.fileName;
				SetProject(prj);
			}
		}

		private void btNewProject_Click(object sender, EventArgs e)
		{
			Project prj = new Project();
			prj.Name = "Новый проект";
			SetProject(prj);
			fileNameProject = "";
		}

		private void tbComment_TextChanged(object sender, EventArgs e)
		{
			if (lbProjects.SelectedIndex >= 0)
			{
				Project.Short p = lbProjects.SelectedValue as Project.Short;
				p.comment = tbComment.Text;
			}
		}

		private void lbProjects_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (lbProjects.SelectedIndex >= 0)
			{
				Project.Short p = lbProjects.SelectedValue as Project.Short;
				tbComment.Text = p.comment;
			}
			else
				tbComment.Text = string.Empty;
		}

		private void btDelete_Click(object sender, EventArgs e)
		{
			if (lbProjects.SelectedIndex >= 0)
			{
				Project.Short p = lbProjects.SelectedValue as Project.Short;
				Project.Short.projects.Remove(p);
				UpdatePageProjects();
			}
		}

		private void cbCameras_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (cbCameras.SelectedValue != null)
			{
				string moniker = cbCameras.SelectedValue as string;
				if (moniker != null)
				{
					if (videoCapture != null)
						videoCapture.Stop();
					videoCapture = new VideoCaptureDevice(moniker);
					videoCapture.NewFrame += videoCapture_NewFrame;
				}
			}
		}

		void videoCapture_NewFrame(object sender, AForge.Video.NewFrameEventArgs e)
		{
			pbImage.Image = (Image)e.Frame.Clone();
		}

		private void chCameraOnOff_CheckedChanged(object sender, EventArgs e)
		{
			if (videoCapture != null)
			{
				if (chCameraOnOff.Checked)
					videoCapture.Start();
				else
					videoCapture.Stop();
			}
		}

		private void btAddImage_Click(object sender, EventArgs e)
		{
			float[] outputs = new float[cbServos.Length];
			for (int i = 0; i < outputs.Length; i++)
				float.TryParse(tbOpers[i].Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out outputs[i]);
			Image image = new Bitmap(pbImage.Image, new Size(640, 480));
			project.AddFrame(tbImageName.Text, image, outputs);
			UpdateListFrames();
			cbImages.SelectedIndex = cbImages.Items.Count - 1;
		}

		private void cbImages_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (cbImages.SelectedValue == null) return;
			Project.Frame frame = (Project.Frame)cbImages.SelectedValue;
			tbImageName.Text = frame.name;
			pbImage.Image = frame.image;
			for (int i = 0; i < tbOpers.Length; i++)
				tbOpers[i].Text = frame.outputs[i].ToString();
		}

		private void btTrain_Click(object sender, EventArgs e)
		{
			UpdateProject();
			System.Threading.Thread th = new System.Threading.Thread(Train);
			th.Start();
		}

		private void pbImage_Paint(object sender, PaintEventArgs e)
		{
			if (chRun.Checked && sendedToServo && (threadSendSignal == null || threadSendSignal.ThreadState == ThreadState.Stopped) )
			{
				Thread th = new Thread(ThreadExecuteImage);
				th.Start(pbImage.Image);
			}
		}

		void ThreadExecuteImage(object o)
		{
			Image img = o as Image;
			float[] outputs = UpdateOutputs(img);
			if (outputs != null)
			{
				SendSignalToServos(outputs);
				Invoke(funcSetOutputs, new object[] { outputs });
			}
		}

		private void btDelImage_Click(object sender, EventArgs e)
		{
			if (cbImages.SelectedValue == null) return;
			if (MessageBox.Show("Вы уверены что этот кадр нужно удалить?", "Вопрос", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
			{
				project.DelFrame((Project.Frame)cbImages.SelectedValue);
				UpdateListFrames();
				cbImages.SelectedIndex = 0;
			}
		}

		private void chRun_CheckedChanged(object sender, EventArgs e)
		{
			if (chRun.Checked)
			{
				UpdateProject();
				if (chRejimA.Checked)
				{
					OnManualRejim();
				}
				else
				{
					sendedToServo = true;
				}
			}
			else
			{
				sendedToServo = false;
				manualControl = 0;
				this.Capture = false;
			}
		}

		private void FormMain_MouseMove(object sender, MouseEventArgs e)
		{
			if (manualControl == 0) return;
			int x = Cursor.Position.X;
			int y = Cursor.Position.Y;
			int sensitivityMouse = (int)udSensitivityMouse.Value;
			int moveMouse = (int)udMoveMouse.Value;
			int sensitivityWheel = (int)udSensitivityWheel.Value;
			int dx = (x - mouseX) / sensitivityMouse;
			int dy = (y - mouseY) / sensitivityMouse;
			float[] delta = new float[manualPosServos.Length];
			bool updated = false;
			if (dx != 0 )
			{
				updated = true;
				delta[0] = dx * moveMouse;
			}
			if (dy != 0)
			{
				updated = true;
				delta[1] = dy * moveMouse;
			}
			if (e.Delta != 0 )
			{
				updated = true;
				if (MouseButtons == System.Windows.Forms.MouseButtons.Left)
				{
					delta[3] = e.Delta / sensitivityWheel;
				}
				else if (MouseButtons == System.Windows.Forms.MouseButtons.Right)
				{
					delta[4] = e.Delta / sensitivityWheel;
				}
				else
				{
					delta[2] = e.Delta / sensitivityWheel;
				}
			}
			if (updated)
			{
				for (int i = 0; i < manualPosServos.Length; i++)
				{
					manualPosServos[i] += delta[i];
					if (manualPosServos[i] < minValueServo) manualPosServos[i] = minValueServo;
					if (manualPosServos[i] > maxValueServo) manualPosServos[i] = maxValueServo;
					if (manualControl == 2) //режим Б
					{
						tbOpers[i].Text = manualPosServos[i].ToString();
					}
				}
				SendSignalToServos(manualPosServos);
				mouseX = x;
				mouseY = y;
				this.Capture = true;
			}
		}

		private void FormMain_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.F1 && chRejimB.Checked)
			{
				if (chRun.Checked)
				{
					chRun.Checked = false;
					chCameraOnOff.Checked = false;
					OnManualRejim();
				}
				else
				{
					this.Capture = false;
					manualControl = 0;
					chRun.Checked = true;
					chCameraOnOff.Checked = true;
				}
			}

		}

		private void FormMain_MouseDown(object sender, MouseEventArgs e)
		{
			if (manualControl != 0)
				this.Capture = true;
		}

		private void chRejimA_CheckedChanged(object sender, EventArgs e)
		{
//			if (chRejimA.Checked)
//				OnManualRejim();
//			else
//				manualControl = 0;
		}

		private void btStopTrain_Click(object sender, EventArgs e)
		{
			stopTrain = true;
		}

	}
}
