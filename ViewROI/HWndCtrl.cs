using System;
using ViewROI;
using System.Collections;
using HalconDotNet;
using System.Collections.Generic;



namespace ViewROI
{
	public delegate void IconicDelegate(int val);
	public delegate void FuncDelegate();

	public class WriteStringViewModel
	{
		public string Text { get; set; }
		public double Row { get; set; }
		public double Col { get; set; }
	}

	public class ArrowViewModel
	{
		public double CenterX { get; set; }
		public double CenterY { get; set; }
		public double FirstArrowX { get; set; }
		public double FirstArrowY { get; set; }
		public double SecArrowX { get; set; }
		public double SecArrowY { get; set; }
	}



	/// <summary>
	/// 縮放事件 Handler
	/// </summary>
	/// <param name="zoomFactor"></param>
	public delegate void ZoomEventHandler(double zoomFactor);

	public delegate void OperationModeChangeEventHandler(string mode);

	/// <summary>
	/// This class works as a wrapper class for the HALCON window
	/// HWindow. HWndCtrl is in charge of the visualization.
	/// You can move and zoom the visible image part by using GUI component 
	/// inputs or with the mouse. The class HWndCtrl uses a graphics stack 
	/// to manage the iconic objects for the display. Each object is linked 
	/// to a graphical context, which determines how the object is to be drawn.
	/// The context can be changed by calling changeGraphicSettings().
	/// The graphical "modes" are defined by the class GraphicsContext and 
	/// map most of the dev_set_* operators provided in HDevelop.
	/// </summary>
	public class HWndCtrl
	{
		/// <summary>
		/// 顯示格線
		/// </summary>
		public bool ShowGrid = false;

		/// <summary>
		/// 水平格線
		/// </summary>
		public int HLines = 0;

		/// <summary>
		/// 垂直格線
		/// </summary>
		public int VLines = 0;

		public List<WriteStringViewModel> WriteStringList = new List<WriteStringViewModel>();
		public List<ArrowViewModel> ArrowList = new List<ArrowViewModel>();

		/// <summary>
		/// 縮放事件
		/// </summary>
		public event ZoomEventHandler OnZoomChanged;

		public event OperationModeChangeEventHandler On_OperationModeChanged;

		/// <summary>No action is performed on mouse events</summary>
		public const int MODE_VIEW_NONE = 10;

		/// <summary>Zoom is performed on mouse events</summary>
		public const int MODE_VIEW_ZOOM = 11;

		/// <summary>Move is performed on mouse events</summary>
		public const int MODE_VIEW_MOVE = 12;

		/// <summary>Magnification is performed on mouse events</summary>
		public const int MODE_VIEW_ZOOMWINDOW = 13;

		/// <summary>
		/// 連續放大鏡模式
		/// </summary>
		public const int MODE_VIEW_ZOOMCONTINUE = 14;

		public const int MODE_INCLUDE_ROI = 1;

		public const int MODE_EXCLUDE_ROI = 2;


		/// <summary>
		/// Constant describes delegate message to signal new image
		/// </summary>
		public const int EVENT_UPDATE_IMAGE = 31;
		/// <summary>
		/// Constant describes delegate message to signal error
		/// when reading an image from file
		/// </summary>
		public const int ERR_READING_IMG = 32;
		/// <summary> 
		/// Constant describes delegate message to signal error
		/// when defining a graphical context
		/// </summary>
		public const int ERR_DEFINING_GC = 33;

		/// <summary> 
		/// Maximum number of HALCON objects that can be put on the graphics 
		/// stack without loss. For each additional object, the first entry 
		/// is removed from the stack again.
		/// </summary>
		private const int MAXNUMOBJLIST = 50;

		/// <summary>
		/// 操作模式
		/// <para>MODE_VIEW_NONE (無)</para>
		/// <para>MODE_VIEW_ZOOM (放大/縮小)</para>
		/// <para>MODE_VIEW_MOVE (移動)</para>
		/// <para>MODE_VIEW_ZOOMWINDOW (放大鏡)</para>
		/// </summary>
		private int _stateView;
		private bool _mousePressed = false;
		private double _moveStartX, _moveStartY;//影像移動模式, 移動起點

		/// <summary>HALCON window</summary>
		private HWindowControl _viewPort;

		/// <summary>
		/// Instance of ROIController, which manages ROI interaction
		/// </summary>
		private ROIController _roiManager;

		/* dispROI is a flag to know when to add the ROI models to the 
		   paint routine and whether or not to respond to mouse events for 
		   ROI objects */
		private int _dispROI;


		/* Basic parameters, like dimension of window and displayed image part */
		private int windowWidth;
		private int windowHeight;
		private int imageWidth;
		private int imageHeight;

		private int[] CompRangeX;
		private int[] CompRangeY;


		private int prevCompX, prevCompY;
		private double stepSizeX, stepSizeY;


		/* Image coordinates, which describe the image part that is displayed  
		   in the HALCON window */
		private double ImgRow1, ImgCol1, ImgRow2, ImgCol2;

		/// <summary>Error message when an exception is thrown</summary>
		public string exceptionText = "";


		/* Delegates to send notification messages to other classes */
		/// <summary>
		/// Delegate to add information to the HALCON window after 
		/// the paint routine has finished
		/// </summary>
		public FuncDelegate addInfoDelegate;

		/// <summary>
		/// Delegate to notify about failed tasks of the HWndCtrl instance
		/// </summary>
		public IconicDelegate NotifyIconObserver;

		/// <summary>
		/// Zoom window default Color
		/// </summary>
		public string ZoomWindowColor = "black";

		/// <summary>
		/// Repatint window default Color
		/// </summary>
		public string RepaintWindowColor = "black";




		private HWindow ZoomWindow;
		private double zoomWndFactor;
		private double zoomAddOn;
		private int zoomWndSize;


		/// <summary> 
		/// List of HALCON objects to be drawn into the HALCON window. 
		/// The list shouldn't contain more than MAXNUMOBJLIST objects, 
		/// otherwise the first entry is removed from the list.
		/// </summary>
		private ArrayList HObjList;

		/// <summary>
		/// Instance that describes the graphical context for the
		/// HALCON window. According on the graphical settings
		/// attached to each HALCON object, this graphical context list 
		/// is updated constantly.
		/// </summary>
		private GraphicsContext mGC;

		private HImage _lastImage;

		/// <summary> 
		/// Initializes the image dimension, mouse delegation, and the 
		/// graphical context setup of the instance.
		/// </summary>
		/// <param name="view"> HALCON window </param>
		public HWndCtrl(HWindowControl view)
		{
			_viewPort = view;
			_stateView = MODE_VIEW_NONE;
			windowWidth = _viewPort.Size.Width;
			windowHeight = _viewPort.Size.Height;

			zoomWndFactor = (double)imageWidth / _viewPort.Width;
			zoomAddOn = Math.Pow(0.9, 5);
			zoomWndSize = 150;

			/*default*/
			CompRangeX = new int[] { 0, 100 };
			CompRangeY = new int[] { 0, 100 };

			prevCompX = prevCompY = 0;

			_dispROI = MODE_INCLUDE_ROI;//1;

			_viewPort.HMouseUp += new HalconDotNet.HMouseEventHandler(this.mouseUp);
			_viewPort.HMouseDown += new HalconDotNet.HMouseEventHandler(this.mouseDown);
			_viewPort.HMouseMove += new HalconDotNet.HMouseEventHandler(this.mouseMoved);

			addInfoDelegate = new FuncDelegate(dummyV);
			NotifyIconObserver = new IconicDelegate(dummy);

			// graphical stack 
			HObjList = new ArrayList(20);
			mGC = new GraphicsContext();
			mGC.gcNotification = new GCDelegate(exceptionGC);
		}


		/// <summary>
		/// Registers an instance of an ROIController with this window 
		/// controller (and vice versa).
		/// </summary>
		/// <param name="rC"> 
		/// Controller that manages interactive ROIs for the HALCON window 
		/// </param>
		public void useROIController(ROIController rC)
		{
			_roiManager = rC;
			rC.setViewController(this);
		}


		/// <summary>
		/// Read dimensions of the image to adjust own window settings
		/// </summary>
		/// <param name="image">HALCON image</param>
		private void setImagePart(HImage image)
		{
			string s;
			int w, h;

			image.GetImagePointer1(out s, out w, out h);
			setImagePart(0, 0, h, w);
		}


		/// <summary>
		/// Adjust window settings by the values supplied for the left 
		/// upper corner and the right lower corner
		/// </summary>
		/// <param name="r1">y coordinate of left upper corner</param>
		/// <param name="c1">x coordinate of left upper corner</param>
		/// <param name="r2">y coordinate of right lower corner</param>
		/// <param name="c2">x coordinate of right lower corner</param>
		private void setImagePart(int r1, int c1, int r2, int c2)
		{
			ImgRow1 = r1;
			ImgCol1 = c1;
			ImgRow2 = imageHeight = r2;
			ImgCol2 = imageWidth = c2;

			System.Drawing.Rectangle rect = _viewPort.ImagePart;
			rect.X = (int)ImgCol1;
			rect.Y = (int)ImgRow1;
			rect.Height = (int)imageHeight;
			rect.Width = (int)imageWidth;
			_viewPort.ImagePart = rect;
		}


		/// <summary>
		/// Sets the view mode for mouse events in the HALCON window
		/// (zoom, move, magnify or none).
		/// </summary>
		/// <param name="mode">One of the MODE_VIEW_* constants</param>
		public void setViewState(int mode)
		{
			if (_stateView == HWndCtrl.MODE_VIEW_ZOOMCONTINUE)
			{
				if (ZoomWindow != null) ZoomWindow.Dispose();
			}
			_stateView = mode;
			if (_roiManager != null)
			{
				_roiManager.resetROI();
			}
			if (On_OperationModeChanged != null)
			{
				var modeText = "";
				switch (mode)
				{
					case HWndCtrl.MODE_VIEW_NONE:
						modeText = "ImageNone";
						break;
					case HWndCtrl.MODE_VIEW_MOVE:
						modeText = "ImageMove";
						break;
					case HWndCtrl.MODE_VIEW_ZOOM:
						modeText = "ImageZoom";
						break;
					case HWndCtrl.MODE_VIEW_ZOOMWINDOW:
						modeText = "ImageManigify";
						break;
				}
				On_OperationModeChanged(modeText);
			}


		}

		/********************************************************************/
		private void dummy(int val)
		{
		}

		private void dummyV()
		{
		}

		/*******************************************************************/
		private void exceptionGC(string message)
		{
			exceptionText = message;
			NotifyIconObserver(ERR_DEFINING_GC);
		}

		/// <summary>
		/// Paint or don't paint the ROIs into the HALCON window by 
		/// defining the parameter to be equal to 1 or not equal to 1.
		/// </summary>
		public void setDispLevel(int mode)
		{
			_dispROI = mode;
		}

		/****************************************************************************/
		/*                          graphical element                               */
		/****************************************************************************/
		private void zoomImage(double x, double y, double scale)
		{
			double lengthC, lengthR;
			double percentC, percentR;
			int lenC, lenR;

			var prevScaleC = (double)((ImgCol2 - ImgCol1) / imageWidth);
			var zoomFactor = prevScaleC * scale;
			if (OnZoomChanged != null)
			{
				OnZoomChanged(zoomFactor);
			}

			percentC = (x - ImgCol1) / (ImgCol2 - ImgCol1);
			percentR = (y - ImgRow1) / (ImgRow2 - ImgRow1);

			lengthC = (ImgCol2 - ImgCol1) * scale;
			lengthR = (ImgRow2 - ImgRow1) * scale;

			ImgCol1 = x - lengthC * percentC;
			ImgCol2 = x + lengthC * (1 - percentC);

			ImgRow1 = y - lengthR * percentR;
			ImgRow2 = y + lengthR * (1 - percentR);

			lenC = (int)Math.Round(lengthC);
			lenR = (int)Math.Round(lengthR);

			System.Drawing.Rectangle rect = _viewPort.ImagePart;
			rect.X = (int)Math.Round(ImgCol1);
			rect.Y = (int)Math.Round(ImgRow1);
			rect.Width = (lenC > 0) ? lenC : 1;
			rect.Height = (lenR > 0) ? lenR : 1;
			_viewPort.ImagePart = rect;

			zoomWndFactor *= scale;
			repaint();
		}

		/// <summary>
		/// Scales the image in the HALCON window according to the 
		/// value scaleFactor
		/// </summary>
		public void zoomImage(double scaleFactor)
		{
			double midPointX, midPointY;

			if (((ImgRow2 - ImgRow1) == scaleFactor * imageHeight) &&
				((ImgCol2 - ImgCol1) == scaleFactor * imageWidth))
			{
				repaint();
				return;
			}

			ImgRow2 = ImgRow1 + imageHeight;
			ImgCol2 = ImgCol1 + imageWidth;

			midPointX = ImgCol1;
			midPointY = ImgRow1;

			zoomWndFactor = (double)imageWidth / _viewPort.Width;
			zoomImage(midPointX, midPointY, scaleFactor);
		}


		/// <summary>
		/// Scales the HALCON window according to the value scale
		/// </summary>
		public void scaleWindow(double scale)
		{
			ImgRow1 = 0;
			ImgCol1 = 0;

			ImgRow2 = imageHeight;
			ImgCol2 = imageWidth;

			_viewPort.Width = (int)(ImgCol2 * scale);
			_viewPort.Height = (int)(ImgRow2 * scale);

			zoomWndFactor = ((double)imageWidth / _viewPort.Width);
		}

		/// <summary>
		/// Recalculates the image-window-factor, which needs to be added to 
		/// the scale factor for zooming an image. This way the zoom gets 
		/// adjusted to the window-image relation, expressed by the equation 
		/// imageWidth/viewPort.Width.
		/// </summary>
		public void setZoomWndFactor()
		{
			zoomWndFactor = ((double)imageWidth / _viewPort.Width);
		}

		/// <summary>
		/// Sets the image-window-factor to the value zoomF
		/// </summary>
		public void setZoomWndFactor(double zoomF)
		{
			zoomWndFactor = zoomF;
		}

		/*******************************************************************/
		private void moveImage(double motionX, double motionY)
		{
			ImgRow1 += -motionY;
			ImgRow2 += -motionY;

			ImgCol1 += -motionX;
			ImgCol2 += -motionX;

			System.Drawing.Rectangle rect = _viewPort.ImagePart;
			rect.X = (int)Math.Round(ImgCol1);
			rect.Y = (int)Math.Round(ImgRow1);
			_viewPort.ImagePart = rect;

			repaint();
		}


		/// <summary>
		/// Resets all parameters that concern the HALCON window display 
		/// setup to their initial values and clears the ROI list.
		/// </summary>
		public void resetAll()
		{
			ImgRow1 = 0;
			ImgCol1 = 0;
			ImgRow2 = imageHeight;
			ImgCol2 = imageWidth;

			zoomWndFactor = (double)imageWidth / _viewPort.Width;

			System.Drawing.Rectangle rect = _viewPort.ImagePart;
			rect.X = (int)ImgCol1;
			rect.Y = (int)ImgRow1;
			rect.Width = (int)imageWidth;
			rect.Height = (int)imageHeight;
			_viewPort.ImagePart = rect;


			if (_roiManager != null)
				_roiManager.reset();
		}

		public void resetWindow()
		{
			ImgRow1 = 0;
			ImgCol1 = 0;
			ImgRow2 = imageHeight;
			ImgCol2 = imageWidth;

			zoomWndFactor = (double)imageWidth / _viewPort.Width;

			System.Drawing.Rectangle rect = _viewPort.ImagePart;
			rect.X = (int)ImgCol1;
			rect.Y = (int)ImgRow1;
			rect.Width = (int)imageWidth;
			rect.Height = (int)imageHeight;
			_viewPort.ImagePart = rect;
			if (OnZoomChanged != null)
			{
				var scale = (double)((ImgCol2 - ImgCol1) / imageWidth);
				OnZoomChanged(scale);
			}
		}


		/*************************************************************************/
		/*      			 Event handling for mouse	   	                     */
		/*************************************************************************/
		private void mouseDown(object sender, HalconDotNet.HMouseEventArgs e)
		{
			_mousePressed = true;
			int activeROIidx = -1;
			double scale;

			if (_roiManager != null && (_dispROI == MODE_INCLUDE_ROI))
			{
				activeROIidx = _roiManager.mouseDownAction(e.X, e.Y);
			}

			if (activeROIidx == -1)
			{
				switch (_stateView)
				{
					case MODE_VIEW_MOVE:
						_moveStartX = e.X;
						_moveStartY = e.Y;
						break;
					case MODE_VIEW_ZOOM:
						if (e.Button == System.Windows.Forms.MouseButtons.Left)
							scale = 0.9;
						else
							scale = 1 / 0.9;
						zoomImage(e.X, e.Y, scale);
						break;
					case MODE_VIEW_NONE:
						break;
					case MODE_VIEW_ZOOMWINDOW:
						activateZoomWindow((int)e.X, (int)e.Y);
						break;
					case MODE_VIEW_ZOOMCONTINUE:
						if (_roiManager != null && (_dispROI == MODE_INCLUDE_ROI))
						{
							activeROIidx = _roiManager.mouseDownAction(e.X, e.Y);
						}
						break;
					default:
						break;
				}
			}
			//end of if
		}

		/*******************************************************************/
		private void activateZoomWindow(int X, int Y)
		{
			double posX, posY;
			int zoomZone;

			if (ZoomWindow != null)
				ZoomWindow.Dispose();

			HOperatorSet.SetSystem("border_width", 10);
			ZoomWindow = new HWindow();

			posX = ((X - ImgCol1) / (ImgCol2 - ImgCol1)) * _viewPort.Width;
			posY = ((Y - ImgRow1) / (ImgRow2 - ImgRow1)) * _viewPort.Height;

			zoomZone = (int)((zoomWndSize / 2) * zoomWndFactor * zoomAddOn);
			ZoomWindow.OpenWindow((int)posY - (zoomWndSize / 2), (int)posX - (zoomWndSize / 2),
								   zoomWndSize, zoomWndSize,
								   _viewPort.HalconID, "visible", "");
			ZoomWindow.SetPart(Y - zoomZone, X - zoomZone, Y + zoomZone, X + zoomZone);
			repaint(ZoomWindow);

			ZoomWindow.SetColor(this.ZoomWindowColor);
		}

		/*******************************************************************/
		private void mouseUp(object sender, HalconDotNet.HMouseEventArgs e)
		{
			_mousePressed = false;

			if (_roiManager != null
				&& (_roiManager.activeROIidx != -1)
				&& (_dispROI == MODE_INCLUDE_ROI))
			{
				_roiManager.NotifyRCObserver(ROIController.EVENT_UPDATE_ROI);
			}
			else if (_stateView == MODE_VIEW_ZOOMWINDOW)
			{
				ZoomWindow.Dispose();
			}
		}

		/*******************************************************************/
		private void mouseMoved(object sender, HalconDotNet.HMouseEventArgs e)
		{
			double motionX, motionY;
			if (_stateView == MODE_VIEW_ZOOMCONTINUE)
			{
				doZoomContinueAction(e);
				return;
			}

			if (!_mousePressed)
				return;

			if (_roiManager != null && (_roiManager.activeROIidx != -1) && (_dispROI == MODE_INCLUDE_ROI))
			{
				_roiManager.mouseMoveAction(e.X, e.Y);
			}
			else if (_stateView == MODE_VIEW_MOVE)
			{
				motionX = ((e.X - _moveStartX));
				motionY = ((e.Y - _moveStartY));

				if (((int)motionX != 0) || ((int)motionY != 0))
				{
					moveImage(motionX, motionY);
					_moveStartX = e.X - motionX;
					_moveStartY = e.Y - motionY;
				}
			}
			else if (_stateView == MODE_VIEW_ZOOMWINDOW)
			{
				if (ZoomWindow != null)
				{
					resetZoomWindow(e);
				}
			}
		}

		/// <summary>
		/// To initialize the move function using a GUI component, the HWndCtrl
		/// first needs to know the range supplied by the GUI component. 
		/// For the x direction it is specified by xRange, which is 
		/// calculated as follows: GuiComponentX.Max()-GuiComponentX.Min().
		/// The starting value of the GUI component has to be supplied 
		/// by the parameter Init
		/// </summary>
		public void setGUICompRangeX(int[] xRange, int Init)
		{
			int cRangeX;

			CompRangeX = xRange;
			cRangeX = xRange[1] - xRange[0];
			prevCompX = Init;
			stepSizeX = ((double)imageWidth / cRangeX) * (imageWidth / windowWidth);

		}

		/// <summary>
		/// To initialize the move function using a GUI component, the HWndCtrl
		/// first needs to know the range supplied by the GUI component. 
		/// For the y direction it is specified by yRange, which is 
		/// calculated as follows: GuiComponentY.Max()-GuiComponentY.Min().
		/// The starting value of the GUI component has to be supplied 
		/// by the parameter Init
		/// </summary>
		public void setGUICompRangeY(int[] yRange, int Init)
		{
			int cRangeY;

			CompRangeY = yRange;
			cRangeY = yRange[1] - yRange[0];
			prevCompY = Init;
			stepSizeY = ((double)imageHeight / cRangeY) * (imageHeight / windowHeight);
		}


		/// <summary>
		/// Resets to the starting value of the GUI component.
		/// </summary>
		public void resetGUIInitValues(int xVal, int yVal)
		{
			prevCompX = xVal;
			prevCompY = yVal;
		}

		/// <summary>
		/// Moves the image by the value valX supplied by the GUI component
		/// </summary>
		public void moveXByGUIHandle(int valX)
		{
			double motionX;

			motionX = (valX - prevCompX) * stepSizeX;

			if (motionX == 0)
				return;

			moveImage(motionX, 0.0);
			prevCompX = valX;
		}


		/// <summary>
		/// Moves the image by the value valY supplied by the GUI component
		/// </summary>
		public void moveYByGUIHandle(int valY)
		{
			double motionY;

			motionY = (valY - prevCompY) * stepSizeY;

			if (motionY == 0)
				return;

			moveImage(0.0, motionY);
			prevCompY = valY;
		}

		/// <summary>
		/// Zooms the image by the value valF supplied by the GUI component
		/// </summary>
		public void zoomByGUIHandle(double valF)
		{
			double x, y, scale;
			double prevScaleC;



			x = (ImgCol1 + (ImgCol2 - ImgCol1) / 2);
			y = (ImgRow1 + (ImgRow2 - ImgRow1) / 2);

			prevScaleC = (double)((ImgCol2 - ImgCol1) / imageWidth);
			scale = ((double)1.0 / prevScaleC * (100.0 / valF));

			zoomImage(x, y, scale);
		}

		/// <summary>
		/// Triggers a repaint of the HALCON window
		/// </summary>
		public void repaint()
		{
			repaint(_viewPort.HalconWindow);
		}

		/// <summary>
		/// Repaints the HALCON window 'window'
		/// </summary>
		public void repaint(HalconDotNet.HWindow window)
		{
			try
			{
				int count = HObjList.Count;
				HObjectEntry entry;

				HSystem.SetSystem("flush_graphic", "false");
				window.ClearWindow();
				mGC.stateOfSettings.Clear();

				for (int i = 0; i < count; i++)
				{
					entry = ((HObjectEntry)HObjList[i]);
					mGC.applyContext(window, entry.gContext);
					window.DispObj(entry.HObj);
				}

				addInfoDelegate();

				if (_roiManager != null && (_dispROI == MODE_INCLUDE_ROI))
					_roiManager.paintData(window);


				var prevScaleC = (double)((ImgCol2 - ImgCol1) / imageWidth);
				foreach (var wViewModel in WriteStringList)
				{
					window.SetColor("red");
					HOperatorSet.SetTposition(window, wViewModel.Row, wViewModel.Col);
					HOperatorSet.WriteString(window, wViewModel.Text);
				}
				//Display Arrow
				var arrowSize = 5 * prevScaleC;
				arrowSize = (arrowSize < 2) ? 2 : arrowSize;
				foreach (var arrowModel in ArrowList)
				{
					HOperatorSet.DispArrow(window, arrowModel.CenterY, arrowModel.CenterX, arrowModel.FirstArrowY, arrowModel.FirstArrowX, arrowSize);
					HOperatorSet.DispArrow(window, arrowModel.CenterY, arrowModel.CenterX, arrowModel.SecArrowY, arrowModel.SecArrowX, arrowSize);
				}

				//畫格線
				if (ShowGrid)
				{
					drawGridLines(window);
				}
				HSystem.SetSystem("flush_graphic", "true");

				window.SetColor(this.RepaintWindowColor);
				window.DispLine(-100.0, -100.0, -101.0, -101.0);
			}
			catch (HOperatorException ex)
			{
				var errorNumber = ex.GetErrorNumber();
				/*
				 5106 發生情境為
				 * 1。開啟擷取影像後，未斷線就關閉 MDI 視窗
				 */
				if (errorNumber != 5106 && errorNumber != 5100)
				{
					Hanbo.Log.LogManager.Error(ex);
				}
			}

		}

		/// <summary>
		/// 畫格線
		/// </summary>
		private void drawGridLines(HalconDotNet.HWindow window)
		{
			if (this.HLines > 0 || this.VLines > 0)
			{
				//算出水平線間隔
				var interval_H = this.imageHeight / (this.HLines + 1);

				//算出垂直線間隔
				var interval_W = this.imageWidth / (this.VLines + 1);

				//設定線段樣式
				HTuple dotLineStyle = new HTuple(new int[4] { 7, 7, 7, 7 });

				HOperatorSet.SetLineStyle(window, dotLineStyle);
				window.SetColor("red");
				//畫水平線
				for (int i = 0; i < this.HLines; i++)
				{
					var rowBegin = (i + 1) * interval_H;
					var rowEnd = rowBegin;

					var colBegin = 0;
					var colEnd = this.imageWidth;
					HOperatorSet.DispLine(window, rowBegin, colBegin, rowEnd, colEnd);
				}
				//畫垂直線
				for (int i = 0; i < this.VLines; i++)
				{
					var rowBegin = 0;
					var rowEnd = this.imageHeight;

					var colBegin = (i + 1) * interval_W;
					var colEnd = colBegin;
					HOperatorSet.DispLine(window, rowBegin, colBegin, rowEnd, colEnd);
				}

				//Reset LineStyle
				HOperatorSet.SetLineStyle(window, null);
			}
		}

		/********************************************************************/
		/*                      GRAPHICSSTACK                               */
		/********************************************************************/

		/// <summary>
		/// Adds an iconic object to the graphics stack similar to the way
		/// it is defined for the HDevelop graphics stack.
		/// </summary>
		/// <param name="obj">Iconic object</param>
		public void addIconicVar(HObject obj)
		{
			//HObjectEntry entry;

			if (obj == null)
				return;

			if (obj is HImage)
			{
				double r, c;
				int h, w, area;
				string s;

				_lastImage = (HImage)obj;

				area = ((HImage)obj).GetDomain().AreaCenter(out r, out c);
				((HImage)obj).GetImagePointer1(out s, out w, out h);

				if (area == (w * h))
				{
					clearList();

					if ((h != imageHeight) || (w != imageWidth))
					{
						imageHeight = h;
						imageWidth = w;
						zoomWndFactor = (double)imageWidth / _viewPort.Width;
						setImagePart(0, 0, h, w);
					}
				}//if
			}//if

			var entry = new HObjectEntry(obj, mGC.copyContextList());

			HObjList.Add(entry);

			if (HObjList.Count > MAXNUMOBJLIST)
				HObjList.RemoveAt(1);
		}

		/// <summary>
		/// 取得最後加入的 HImage
		/// </summary>
		/// <returns></returns>
		public HImage GetLastHImage()
		{
			return _lastImage;
		}


		/// <summary>
		/// Clears all entries from the graphics stack 
		/// </summary>
		public void clearList()
		{
			HObjList.Clear();
		}

		/// <summary>
		/// Returns the number of items on the graphics stack
		/// </summary>
		public int getListCount()
		{
			return HObjList.Count;
		}

		/// <summary>
		/// Changes the current graphical context by setting the specified mode
		/// (constant starting by GC_*) to the specified value.
		/// </summary>
		/// <param name="mode">
		/// Constant that is provided by the class GraphicsContext
		/// and describes the mode that has to be changed, 
		/// e.g., GraphicsContext.GC_COLOR
		/// </param>
		/// <param name="val">
		/// Value, provided as a string, 
		/// the mode is to be changed to, e.g., "blue" 
		/// </param>
		public void changeGraphicSettings(string mode, string val)
		{
			switch (mode)
			{
				case GraphicsContext.GC_COLOR:
					mGC.setColorAttribute(val);
					break;
				case GraphicsContext.GC_DRAWMODE:
					mGC.setDrawModeAttribute(val);
					break;
				case GraphicsContext.GC_LUT:
					mGC.setLutAttribute(val);
					break;
				case GraphicsContext.GC_PAINT:
					mGC.setPaintAttribute(val);
					break;
				case GraphicsContext.GC_SHAPE:
					mGC.setShapeAttribute(val);
					break;
				default:
					break;
			}
		}

		/// <summary>
		/// Changes the current graphical context by setting the specified mode
		/// (constant starting by GC_*) to the specified value.
		/// </summary>
		/// <param name="mode">
		/// Constant that is provided by the class GraphicsContext
		/// and describes the mode that has to be changed, 
		/// e.g., GraphicsContext.GC_LINEWIDTH
		/// </param>
		/// <param name="val">
		/// Value, provided as an integer, the mode is to be changed to, 
		/// e.g., 5 
		/// </param>
		public void changeGraphicSettings(string mode, int val)
		{
			switch (mode)
			{
				case GraphicsContext.GC_COLORED:
					mGC.setColoredAttribute(val);
					break;
				case GraphicsContext.GC_LINEWIDTH:
					mGC.setLineWidthAttribute(val);
					break;
				default:
					break;
			}
		}

		/// <summary>
		/// Changes the current graphical context by setting the specified mode
		/// (constant starting by GC_*) to the specified value.
		/// </summary>
		/// <param name="mode">
		/// Constant that is provided by the class GraphicsContext
		/// and describes the mode that has to be changed, 
		/// e.g.,  GraphicsContext.GC_LINESTYLE
		/// </param>
		/// <param name="val">
		/// Value, provided as an HTuple instance, the mode is 
		/// to be changed to, e.g., new HTuple(new int[]{2,2})
		/// </param>
		public void changeGraphicSettings(string mode, HTuple val)
		{
			switch (mode)
			{
				case GraphicsContext.GC_LINESTYLE:
					mGC.setLineStyleAttribute(val);
					break;
				default:
					break;
			}
		}

		/// <summary>
		/// Clears all entries from the graphical context list
		/// </summary>
		public void clearGraphicContext()
		{
			mGC.clear();
		}

		/// <summary>
		/// Returns a clone of the graphical context list (hashtable)
		/// </summary>
		public Hashtable getGraphicContext()
		{
			return mGC.copyContextList();
		}

		/// <summary>
		/// Clear writeStrings and arrows
		/// </summary>
		public void ClearTextAndArrows()
		{
			this.WriteStringList.Clear();
			this.ArrowList.Clear();
			this.repaint();
		}

		#region 連續放大模式
		/// <summary>
		/// 連續使用放大鏡 flag
		/// </summary>
		private bool _enableZoomContinue = false;

		/// <summary>
		/// 保留模式
		/// <para>預設為 -1</para>
		/// </summary>
		private int _preservedStateView = -1;
		/// <summary>
		/// 連續放大鏡模式啟用
		/// </summary>
		/// <param name="flag"></param>
		public void EnableZoomContinue()
		{
			_preservedStateView = _stateView;
			this.setViewState(HWndCtrl.MODE_VIEW_ZOOMCONTINUE);
			_enableZoomContinue = true;
		}
		/// <summary>
		/// 停用連續放大鏡模式
		/// </summary>
		public void DisableZoomContinue()
		{
			_enableZoomContinue = false;
			if (_preservedStateView > -1)
			{
				this.setViewState(_preservedStateView);
				_preservedStateView = -1;
				_mousePressed = false;
			}
		}

		/// <summary>
		/// 連續放大鏡模式 Action
		/// </summary>
		/// <param name="e"></param>
		private void doZoomContinueAction(HalconDotNet.HMouseEventArgs e)
		{
			if (_enableZoomContinue)
			{
				activateZoomWindow((int)e.X, (int)e.Y);
				_enableZoomContinue = false;
			}
			if (ZoomWindow != null)
			{
				resetZoomWindow(e);
				ZoomWindow.SetColor("red");
				ZoomWindow.DispCross(e.Y, e.X, 16.0, 0.785398);
				ZoomWindow.SetColor(this.ZoomWindowColor);
			}
		}
		/// <summary>
		/// 重置放大鏡視窗
		/// </summary>
		/// <param name="e"></param>
		private void resetZoomWindow(HalconDotNet.HMouseEventArgs e)
		{
			HSystem.SetSystem("flush_graphic", "false");
			ZoomWindow.ClearWindow();
			double posX = ((e.X - ImgCol1) / (ImgCol2 - ImgCol1)) * _viewPort.Width;
			double posY = ((e.Y - ImgRow1) / (ImgRow2 - ImgRow1)) * _viewPort.Height;
			double zoomZone = (zoomWndSize / 2) * zoomWndFactor * zoomAddOn;

			ZoomWindow.SetWindowExtents((int)posY - (zoomWndSize / 2),
										(int)posX - (zoomWndSize / 2),
										zoomWndSize, zoomWndSize);
			ZoomWindow.SetPart((int)(e.Y - zoomZone), (int)(e.X - zoomZone),
							   (int)(e.Y + zoomZone), (int)(e.X + zoomZone));

			repaint(ZoomWindow);

			HSystem.SetSystem("flush_graphic", "true");
			ZoomWindow.DispLine(-100.0, -100.0, -100.0, -100.0);
		}
		#endregion

	}//end of class
}//end of namespace
