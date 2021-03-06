﻿/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace FlowSharpLib
{
	public static class GraphicElementHelpers
	{
		public static void Erase(this Bitmap background, Canvas canvas, Rectangle r)
		{
			canvas.DrawImage(background, r);
			background.Dispose();
		}
	}

	public class PropertiesChangedEventArgs : EventArgs
	{
		public GraphicElement GraphicElement { get; protected set; }

		public PropertiesChangedEventArgs(GraphicElement el)
		{
			GraphicElement = el;
		}
	}

	public class GraphicElement : IDisposable
    {
		public EventHandler<PropertiesChangedEventArgs> PropertiesChanged;

		public Guid Id { get; set; }
		public virtual bool Selected { get; set; }
		public bool ShowConnectionPoints { get; set; }
		// public bool HideConnectionPoints { get; set; }
		public bool ShowAnchors { get; set; }
		public Canvas Canvas { get { return canvas; } }

		// This is probably a ridiculous optimization -- should just grow pen width + connection point size / 2
		// public virtual Rectangle UpdateRectangle { get { return DisplayRectangle.Grow(BorderPen.Width + ((ShowConnectionPoints || HideConnectionPoints) ? 3 : 0)); } }
		public virtual Rectangle UpdateRectangle { get { return DisplayRectangle.Grow(BorderPen.Width + BaseController.CONNECTION_POINT_SIZE); } }
        public virtual bool IsConnector { get { return false; } }
		public List<Connection> Connections = new List<Connection>();

		public Rectangle DisplayRectangle { get; set; }
		public Pen BorderPen { get; set; }
        public SolidBrush FillBrush { get; set; }

		public string Text { get; set; }
		public Font TextFont { get; set; }
		public Color TextColor { get; set; }
		// TODO: Text location - left, top, right, middle, bottom

		protected bool HasCornerAnchors { get; set; }
		protected bool HasCenterAnchors { get; set; }
		protected bool HasLeftRightAnchors { get; set; }
		protected bool HasTopBottomAnchors { get; set; }

		protected bool HasCornerConnections { get; set; }
		protected bool HasCenterConnections { get; set; }
		protected bool HasLeftRightConnections { get; set; }
		protected bool HasTopBottomConnections { get; set; }

		protected Bitmap background;
        protected Rectangle backgroundRectangle;
        protected Pen selectionPen;
		protected Pen altSelectionPen;
		protected Pen anchorPen = new Pen(Color.Black);
		protected Pen connectionPointPen = new Pen(Color.Blue);
		protected SolidBrush anchorBrush = new SolidBrush(Color.White);
		protected int anchorWidthHeight = 6;		// TODO: Make const?
		protected Canvas canvas;

		protected bool disposed;

        public GraphicElement(Canvas canvas)
        {
			Id = Guid.NewGuid();
			this.canvas = canvas;
            selectionPen = new Pen(Color.Red);
			altSelectionPen = new Pen(Color.Blue);
			HasCenterAnchors = true;
			HasCornerAnchors = true;
			HasLeftRightAnchors = false;
			HasTopBottomAnchors = false;
			HasCenterConnections = true;
			HasCornerConnections = true;
			HasLeftRightConnections = false;
			HasTopBottomConnections = false;
			FillBrush = new SolidBrush(Color.White);
			BorderPen = new Pen(Color.Black);
			BorderPen.Width = 1;
			TextFont = new Font(FontFamily.GenericSansSerif, 10);
			TextColor = Color.Black;
		}

        public override string ToString()
        {
            return GetType().Name + " (" + Id + ") : " + Text;
        }

        public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				disposed = true;

				if (disposing)
				{
					BorderPen.Dispose();
					FillBrush.Dispose();
					background?.Dispose();
					selectionPen.Dispose();
					altSelectionPen.Dispose();
					anchorPen.Dispose();
					anchorBrush.Dispose();
					TextFont.Dispose();
                    connectionPointPen.Dispose();
				}
			}
		}

		// TODO: Unify these into the second form at the call site.
		public virtual void MoveAnchor(ConnectionPoint cpShape, ConnectionPoint tocp) { }
		public virtual void MoveAnchor(GripType type, Point delta) { }

		public virtual ElementProperties CreateProperties()
		{
			return new ShapeProperties(this);
		}

		public virtual Rectangle DefaultRectangle()
		{
			return new Rectangle(20, 20, 60, 60);
		}

		public virtual bool IsSelectable(Point p)
		{
			return UpdateRectangle.Contains(p);
		}

        public virtual GraphicElement CloneDefault(Canvas canvas)
        {
            return CloneDefault(canvas, Point.Empty);
        }

        /// <summary>
        /// Clone onto the specified canvas the default shape.
        /// </summary>
        public virtual GraphicElement CloneDefault(Canvas canvas, Point offset)
		{
			GraphicElement el = (GraphicElement)Activator.CreateInstance(GetType(), new object[] { canvas });
            el.DisplayRectangle = el.DefaultRectangle();
            el.Move(offset);
            el.UpdateProperties();
			el.UpdatePath();

			return el;
		}

		public virtual void Serialize(ElementPropertyBag epb)
		{
			epb.ElementName = GetType().AssemblyQualifiedName;
			epb.Id = Id;
			epb.DisplayRectangle = DisplayRectangle;
			epb.BorderPenColor = BorderPen.Color;
			epb.BorderPenWidth = (int)BorderPen.Width;
			epb.FillBrushColor = FillBrush.Color;
			epb.Text = Text;
			epb.TextColor = TextColor;
			epb.TextFontFamily = TextFont.FontFamily.Name;
			epb.TextFontSize = TextFont.Size;
			epb.TextFontUnderline = TextFont.Underline;
			epb.TextFontStrikeout = TextFont.Strikeout;
			epb.TextFontItalic = TextFont.Italic;

			epb.HasCornerAnchors = HasCornerAnchors;
			epb.HasCenterAnchors = HasCenterAnchors;
			epb.HasLeftRightAnchors = HasLeftRightAnchors;
			epb.HasTopBottomAnchors = HasTopBottomAnchors;

			epb.HasCornerConnections = HasCornerConnections;
			epb.HasCenterConnections = HasCenterConnections;
			epb.HasLeftRightConnections = HasLeftRightConnections;
			epb.HasTopBottomConnections = HasTopBottomConnections;

			Connections.ForEach(c => c.Serialize(epb));
		}

		public virtual void Deserialize(ElementPropertyBag epb)
		{
			Id = epb.Id;
			DisplayRectangle = epb.DisplayRectangle;
			BorderPen.Dispose();
			BorderPen = new Pen(epb.BorderPenColor, epb.BorderPenWidth);
			FillBrush.Dispose();
			FillBrush = new SolidBrush(epb.FillBrushColor);
			Text = epb.Text;
			TextColor = epb.TextColor;
			TextFont.Dispose();
			FontStyle fontStyle = (epb.TextFontUnderline ? FontStyle.Underline : FontStyle.Regular) | (epb.TextFontItalic ? FontStyle.Italic : FontStyle.Regular) | (epb.TextFontStrikeout ? FontStyle.Strikeout : FontStyle.Regular);
			TextFont = new Font(epb.TextFontFamily, epb.TextFontSize, fontStyle);

			HasCornerAnchors = epb.HasCornerAnchors;
			HasCenterAnchors = epb.HasCenterAnchors;
			HasLeftRightAnchors = epb.HasLeftRightAnchors;
			HasTopBottomAnchors = epb.HasTopBottomAnchors;

			HasCornerConnections = epb.HasCornerConnections;
			HasCenterConnections = epb.HasCenterConnections;
			HasLeftRightConnections = epb.HasLeftRightConnections;
			HasTopBottomConnections = epb.HasTopBottomConnections;
		}

        public virtual void FinalFixup(List<GraphicElement> elements, ElementPropertyBag epb, Dictionary<Guid, Guid> oldNewGuidMap)
        {
            elements.ForEach(el => el.UpdateProperties());
        }

		public bool OnScreen(Rectangle r)
		{
			return canvas.OnScreen(r);
		}

		public bool OnScreen()
		{
			return canvas.OnScreen(UpdateRectangle);
		}

		public bool OnScreen(int dx, int dy)
		{
			return canvas.OnScreen(UpdateRectangle.Grow(dx, dy));
		}

		public virtual void UpdatePath() { }

        public virtual void Move(Point delta)
        {
            DisplayRectangle = DisplayRectangle.Move(delta);
        }

		public virtual void UpdateSize(ShapeAnchor anchor, Point delta)
		{
			canvas.Controller.UpdateSize(this, anchor, delta);
		}

        public virtual void GetBackground()
        {
            background?.Dispose();
			background = null;
			backgroundRectangle = canvas.Clip(UpdateRectangle);

			if (canvas.OnScreen(backgroundRectangle))
			{
				background = canvas.GetImage(backgroundRectangle);
			}
        }

        public virtual void CancelBackground()
        {
            background?.Dispose();
            background = null;
        }

		public virtual bool SnapCheck(ShapeAnchor anchor, Point delta)
		{
			UpdateSize(anchor, delta);
			canvas.Controller.UpdateSelectedElement.Fire(this, new ElementEventArgs() { Element = this });

			return false;
		}

		// Default returns true so we don't detach a shape's connectors when moving a shape.
		public virtual bool SnapCheck(GripType gt, ref Point delta) { return false; }

		// Placeholders:
		public virtual void MoveElementOrAnchor(GripType gt, Point delta) { }
		public virtual void SetConnection(GripType gt, GraphicElement shape) { }
		public virtual void DisconnectShapeFromConnector(GripType gt) { }
		public virtual void DetachAll() { }
		public virtual void UpdateProperties() { }

        public virtual void RemoveConnection(GripType gt)
        {
            // Connections.SingleOrDefault(c=>c.ElementConnectionPoint.Type == gr)
        }

        public virtual void SetCanvas(Canvas canvas)
		{
			this.canvas = canvas;
		}

		public virtual void Erase()
        {
            if (canvas.OnScreen(backgroundRectangle))
            {
                Trace.WriteLine("Erase " + ToString());
                background?.Erase(canvas, backgroundRectangle);
                // canvas.Graphics.DrawRectangle(selectionPen, backgroundRectangle);
                background = null;
            }
        }

        public virtual void Draw()
        {
			Graphics gr = canvas.AntiAliasGraphics;

            if (canvas.OnScreen(UpdateRectangle))
            {
                Trace.WriteLine("Draw " + ToString());
                Draw(gr);
            }

			if (ShowAnchors)
			{
				DrawAnchors(gr);
			}

			if (ShowConnectionPoints)
			{
				DrawConnectionPoints(gr);
			}

			if (!String.IsNullOrEmpty(Text))
			{
				DrawText(gr);
			}
        }

		public virtual void UpdateScreen(int ix = 0, int iy = 0)
		{
			Rectangle r = canvas.Clip(UpdateRectangle.Grow(ix, iy));

			if (canvas.OnScreen(r))
			{
				canvas.CopyToScreen(r);
			}
		}

        public virtual ShapeAnchor GetBottomRightAnchor()
        {
            Size anchorSize = new Size(anchorWidthHeight, anchorWidthHeight);
            Rectangle r = new Rectangle(DisplayRectangle.BottomRightCorner().Move(-anchorWidthHeight, -anchorWidthHeight), anchorSize);
            ShapeAnchor anchor = new ShapeAnchor(GripType.BottomRight, r, Cursors.SizeNWSE);

            return anchor;
        }

        public virtual List<ShapeAnchor> GetAnchors()
		{
			List<ShapeAnchor> anchors = new List<ShapeAnchor>();
			Rectangle r;
            Size anchorSize = new Size(anchorWidthHeight, anchorWidthHeight);

			if (HasCornerAnchors)
			{
				r = new Rectangle(DisplayRectangle.TopLeftCorner(), anchorSize);
				anchors.Add(new ShapeAnchor(GripType.TopLeft, r, Cursors.SizeNWSE));
				r = new Rectangle(DisplayRectangle.TopRightCorner().Move(-anchorWidthHeight, 0), anchorSize);
				anchors.Add(new ShapeAnchor(GripType.TopRight, r, Cursors.SizeNESW));
				r = new Rectangle(DisplayRectangle.BottomLeftCorner().Move(0, -anchorWidthHeight), anchorSize);
				anchors.Add(new ShapeAnchor(GripType.BottomLeft, r, Cursors.SizeNESW));
				r = new Rectangle(DisplayRectangle.BottomRightCorner().Move(-anchorWidthHeight, -anchorWidthHeight), anchorSize);
				anchors.Add(new ShapeAnchor(GripType.BottomRight, r, Cursors.SizeNWSE));
			}

			if (HasCenterAnchors || HasLeftRightAnchors)
			{
				r = new Rectangle(DisplayRectangle.LeftMiddle().Move(0, -anchorWidthHeight / 2), anchorSize);
				anchors.Add(new ShapeAnchor(GripType.LeftMiddle, r, Cursors.SizeWE));
				r = new Rectangle(DisplayRectangle.RightMiddle().Move(-anchorWidthHeight, -anchorWidthHeight / 2), anchorSize);
				anchors.Add(new ShapeAnchor(GripType.RightMiddle, r, Cursors.SizeWE));
			}

			if (HasCenterAnchors || HasTopBottomAnchors)
			{ 
				r = new Rectangle(DisplayRectangle.TopMiddle().Move(-anchorWidthHeight / 2, 0), anchorSize);
				anchors.Add(new ShapeAnchor(GripType.TopMiddle, r, Cursors.SizeNS));
				r = new Rectangle(DisplayRectangle.BottomMiddle().Move(-anchorWidthHeight / 2, -anchorWidthHeight), anchorSize);
				anchors.Add(new ShapeAnchor(GripType.BottomMiddle, r, Cursors.SizeNS));
			}

			return anchors;
		}

		public virtual List<ConnectionPoint> GetConnectionPoints()
		{
			List<ConnectionPoint> connectionPoints = new List<ConnectionPoint>();

			if (HasCornerConnections)
			{
				connectionPoints.Add(new ConnectionPoint(GripType.TopLeft, DisplayRectangle.TopLeftCorner()));
				connectionPoints.Add(new ConnectionPoint(GripType.TopRight, DisplayRectangle.TopRightCorner()));
				connectionPoints.Add(new ConnectionPoint(GripType.BottomLeft, DisplayRectangle.BottomLeftCorner()));
				connectionPoints.Add(new ConnectionPoint(GripType.BottomRight, DisplayRectangle.BottomRightCorner()));
			}

			if (HasCenterConnections)
			{
				connectionPoints.Add(new ConnectionPoint(GripType.LeftMiddle, DisplayRectangle.LeftMiddle()));
				connectionPoints.Add(new ConnectionPoint(GripType.RightMiddle, DisplayRectangle.RightMiddle()));
				connectionPoints.Add(new ConnectionPoint(GripType.TopMiddle, DisplayRectangle.TopMiddle()));
				connectionPoints.Add(new ConnectionPoint(GripType.BottomMiddle, DisplayRectangle.BottomMiddle()));
			}

			if (HasLeftRightConnections)
			{
				connectionPoints.Add(new ConnectionPoint(GripType.Start, DisplayRectangle.LeftMiddle()));
				connectionPoints.Add(new ConnectionPoint(GripType.End, DisplayRectangle.RightMiddle()));
			}

			if (HasTopBottomConnections)
			{
				connectionPoints.Add(new ConnectionPoint(GripType.Start, DisplayRectangle.TopMiddle()));
				connectionPoints.Add(new ConnectionPoint(GripType.End, DisplayRectangle.BottomMiddle()));
			}

			return connectionPoints;
		}

		public virtual void Draw(Graphics gr)
        {
            if (Selected)
            {
				DrawSelection(gr);
            }

			// For illustration / debugging of what's being updated.
			// DrawUpdateRectangle(gr);
        }

		protected virtual void DrawSelection(Graphics gr)
		{
			if (BorderPen.Color.ToArgb() == selectionPen.Color.ToArgb())
			{
				Rectangle r = DisplayRectangle;
				gr.DrawRectangle(altSelectionPen, r);
			}
			else
			{
				Rectangle r = DisplayRectangle;
				gr.DrawRectangle(selectionPen, r);
			}
		}

		// For illustration / debugging of what's being updated.
		protected virtual void DrawUpdateRectangle(Graphics gr)
		{
			Pen pen = new Pen(Color.Gray);
			Rectangle r = UpdateRectangle.Grow(-1);
			gr.DrawRectangle(pen, r);
			pen.Dispose();
		}

		protected virtual void DrawAnchors(Graphics gr)
		{
			GetAnchors().ForEach((a =>
			{
				gr.DrawRectangle(anchorPen, a.Rectangle);
				gr.FillRectangle(anchorBrush, a.Rectangle.Grow(-1));
			}));
		}

		protected virtual void DrawConnectionPoints(Graphics gr)
		{
			GetConnectionPoints().ForEach(cp =>
			{
				gr.FillRectangle(anchorBrush, new Rectangle(cp.Point.X - BaseController.CONNECTION_POINT_SIZE, cp.Point.Y - BaseController.CONNECTION_POINT_SIZE, BaseController.CONNECTION_POINT_SIZE*2, BaseController.CONNECTION_POINT_SIZE*2));
				gr.DrawLine(connectionPointPen, cp.Point.X - BaseController.CONNECTION_POINT_SIZE, cp.Point.Y - BaseController.CONNECTION_POINT_SIZE, cp.Point.X + BaseController.CONNECTION_POINT_SIZE, cp.Point.Y + BaseController.CONNECTION_POINT_SIZE);
				gr.DrawLine(connectionPointPen, cp.Point.X + BaseController.CONNECTION_POINT_SIZE, cp.Point.Y - BaseController.CONNECTION_POINT_SIZE, cp.Point.X - BaseController.CONNECTION_POINT_SIZE, cp.Point.Y + BaseController.CONNECTION_POINT_SIZE);
			});
		}

		public virtual void DrawText(Graphics gr)
		{
			SizeF size = gr.MeasureString(Text, TextFont);
			Point textpos = DisplayRectangle.Center().Move((int)(-size.Width / 2), (int)(-size.Height / 2));
			Brush brush = new SolidBrush(TextColor);
			gr.DrawString(Text, TextFont, brush, textpos);
			brush.Dispose();
		}
	}
}
