using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DimScreen
{
    /// <summary>
    /// A GlassPanelForm sits transparently over an ObjectListView to show overlays.
    /// </summary>
    internal partial class GlassPanelForm : Form
    {
        public GlassPanelForm()
        {
            this.Name = "GlassPanelForm";
            this.Text = "GlassPanelForm";

            ClientSize = new System.Drawing.Size(0, 0);
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.Manual;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;

            SetStyle(ControlStyles.Selectable, false);

            this.Opacity = 0.5f;
            this.BackColor = Color.FromArgb(255, 254, 254, 254);
            this.TransparencyKey = this.BackColor;
            this.HideGlass();
            NativeMethods.ShowWithoutActivate(this);
        }

        #region Properties

        /// <summary>
        /// Get the low-level windows flag that will be given to CreateWindow.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW 
                return cp;
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Attach this form to the given ObjectListView
        /// </summary>        
        public void Bind(Form owner)
        {
            if (owner == null) return;

            this.mdiClient = null;
            this.mdiOwner = null;

            //Control parent = this.objectListView.Parent;
            //while (parent != null)
            //{
            //    parent.ParentChanged += new EventHandler(objectListView_ParentChanged);
            //    TabControl tabControl = parent as TabControl;
            //    if (tabControl != null)
            //    {
            //        tabControl.Selected += new TabControlEventHandler(tabControl_Selected);
            //    }
            //    parent = parent.Parent;
            //}
            this.Owner = owner;
            this.myOwner = this.Owner;
            if (this.Owner != null)
            {
                this.Owner.LocationChanged += new EventHandler(Owner_LocationChanged);
                this.Owner.SizeChanged += new EventHandler(Owner_SizeChanged);
                this.Owner.ResizeBegin += new EventHandler(Owner_ResizeBegin);
                this.Owner.ResizeEnd += new EventHandler(Owner_ResizeEnd);
                if (this.Owner.TopMost)
                {
                    // We can't do this.TopMost = true; since that will activate the panel,
                    // taking focus away from the owner of the listview
                    NativeMethods.MakeTopMost(this);
                }

                // We need special code to handle MDI
                this.mdiOwner = this.Owner.MdiParent;
                if (this.mdiOwner != null)
                {
                    this.mdiOwner.LocationChanged += new EventHandler(Owner_LocationChanged);
                    this.mdiOwner.SizeChanged += new EventHandler(Owner_SizeChanged);
                    this.mdiOwner.ResizeBegin += new EventHandler(Owner_ResizeBegin);
                    this.mdiOwner.ResizeEnd += new EventHandler(Owner_ResizeEnd);

                    // Find the MDIClient control, which houses all MDI children
                    foreach (Control c in this.mdiOwner.Controls)
                    {
                        this.mdiClient = c as MdiClient;
                        if (this.mdiClient != null)
                        {
                            break;
                        }
                    }
                    if (this.mdiClient != null)
                    {
                        this.mdiClient.ClientSizeChanged += new EventHandler(myMdiClient_ClientSizeChanged);
                    }
                }
            }

            this.UpdateTransparency();
        }

        void myMdiClient_ClientSizeChanged(object sender, EventArgs e)
        {
            this.RecalculateBounds();
            this.Invalidate();
        }

        MdiClient mdiClient;
        /// <summary>
        /// Made the overlay panel invisible
        /// </summary>
        public void HideGlass()
        {
            if (!this.isGlassShown)
                return;
            this.isGlassShown = false;
            this.Bounds = new Rectangle(-10000, -10000, 1, 1);
        }

        /// <summary>
        /// Show the overlay panel in its correctly location
        /// </summary>
        /// <remarks>
        /// If the panel is always shown, this method does nothing.
        /// If the panel is being resized, this method also does nothing.
        /// </remarks>
        public void ShowGlass()
        {
            if (this.isGlassShown || this.isDuringResizeSequence)
                return;

            this.isGlassShown = true;
            this.RecalculateBounds();
        }

        /// <summary>
        /// Detach this glass panel from its previous ObjectListView
        /// </summary>        
        /// <remarks>
        /// You should unbind the overlay panel before making any changes to the 
        /// widget hierarchy.
        /// </remarks>
        public void Unbind()
        {

            if (this.myOwner != null)
            {
                this.myOwner.LocationChanged -= new EventHandler(Owner_LocationChanged);
                this.myOwner.SizeChanged -= new EventHandler(Owner_SizeChanged);
                this.myOwner.ResizeBegin -= new EventHandler(Owner_ResizeBegin);
                this.myOwner.ResizeEnd -= new EventHandler(Owner_ResizeEnd);
            }

            if (this.mdiOwner != null)
            {
                this.mdiOwner.LocationChanged -= new EventHandler(Owner_LocationChanged);
                this.mdiOwner.SizeChanged -= new EventHandler(Owner_SizeChanged);
                this.mdiOwner.ResizeBegin -= new EventHandler(Owner_ResizeBegin);
                this.mdiOwner.ResizeEnd -= new EventHandler(Owner_ResizeEnd);
            }

            if (this.mdiClient != null)
            {
                this.mdiClient.ClientSizeChanged -= new EventHandler(myMdiClient_ClientSizeChanged);
            }
        }

        #endregion

        #region Event Handlers

        void objectListView_Disposed(object sender, EventArgs e)
        {
            this.Unbind();
        }

        /// <summary>
        /// Handle when the form that owns the ObjectListView begins to be resized
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Owner_ResizeBegin(object sender, EventArgs e)
        {
            // When the top level window is being resized, we just want to hide
            // the overlay window. When the resizing finishes, we want to show
            // the overlay window, if it was shown before the resize started.
            this.isDuringResizeSequence = true;
            this.wasGlassShownBeforeResize = this.isGlassShown;
        }

        /// <summary>
        /// Handle when the form that owns the ObjectListView finished to be resized
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Owner_ResizeEnd(object sender, EventArgs e)
        {
            this.isDuringResizeSequence = false;
            if (this.wasGlassShownBeforeResize)
                this.ShowGlass();
        }

        /// <summary>
        /// The owning form has moved. Move the overlay panel too.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Owner_LocationChanged(object sender, EventArgs e)
        {
            if (this.mdiOwner != null)
                this.HideGlass();
            else
                this.RecalculateBounds();
        }

        /// <summary>
        /// The owning form is resizing. Hide our overlay panel until the resizing stops
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Owner_SizeChanged(object sender, EventArgs e)
        {
            this.HideGlass();
        }


        /// <summary>
        /// Handle when the bound OLV changes its location. The overlay panel must 
        /// be moved too, IFF it is currently visible.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void objectListView_LocationChanged(object sender, EventArgs e)
        {
            if (this.isGlassShown)
            {
                this.RecalculateBounds();
            }
        }

        /// <summary>
        /// Handle when the bound OLV changes size. The overlay panel must 
        /// resize too, IFF it is currently visible.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void objectListView_SizeChanged(object sender, EventArgs e)
        {
            // This event is triggered in all sorts of places, and not always when the size changes.
            //if (this.isGlassShown) {
            //    this.Size = this.objectListView.ClientSize;
            //}
        }

        /// <summary>
        /// Handle when the bound OLV is part of a TabControl and that
        /// TabControl changes tabs. The overlay panel is hidden. The
        /// first time the bound OLV is redrawn, the overlay panel will
        /// be shown again.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void tabControl_Selected(object sender, TabControlEventArgs e)
        {
            this.HideGlass();
        }

        /// <summary>
        /// Somewhere the parent of the bound OLV has changed. Update
        /// our events.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void objectListView_ParentChanged(object sender, EventArgs e)
        {
            this.Unbind();
            //this.Bind(olv, overlay);
        }

        /// <summary>
        /// Handle when the bound OLV changes its visibility.
        /// The overlay panel should match the OLV's visibility.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void objectListView_VisibleChanged(object sender, EventArgs e)
        {
            if (this.Owner.Visible)
                this.ShowGlass();
            else
                this.HideGlass();
        }

        #endregion

        #region Implementation

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            //g.TextRenderingHint = ObjectListView.TextRenderingHint;
            //g.SmoothingMode = ObjectListView.SmoothingMode;
            //g.DrawRectangle(new Pen(Color.Green, 4.0f), this.ClientRectangle);

            // If we are part of an MDI app, make sure we don't draw outside the bounds
            //if (this.mdiClient != null)
            //{
            //    Rectangle r = mdiClient.RectangleToScreen(mdiClient.ClientRectangle);
            //    Rectangle r2 = this.objectListView.RectangleToClient(r);
            //    g.SetClip(r2, System.Drawing.Drawing2D.CombineMode.Intersect);
            //}

            //this.Overlay.Draw(this.objectListView, g, this.objectListView.ClientRectangle);
            g.FillRectangle(new SolidBrush(Color.Black), this.Owner.ClientRectangle);
        }

        protected void RecalculateBounds()
        {
            if (!this.isGlassShown)
                return;

            Rectangle rect = this.Owner.ClientRectangle;
            rect.X = 0;
            rect.Y = 0;
            this.Bounds = this.Owner.RectangleToScreen(rect);
        }

        internal void UpdateTransparency()
        {
            //ITransparentOverlay transparentOverlay = this.Overlay as ITransparentOverlay;
            //if (transparentOverlay == null)
            //    this.Opacity = this.objectListView.OverlayTransparency / 255.0f;
            //else
            //    this.Opacity = transparentOverlay.Transparency / 255.0f;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 132;
            const int HTTRANSPARENT = -1;
            switch (m.Msg)
            {
                // Ignore all mouse interactions
                case WM_NCHITTEST:
                    m.Result = (IntPtr)HTTRANSPARENT;
                    break;
            }
            base.WndProc(ref m);
        }

        #endregion

        #region Implementation variables


        #endregion

        #region Private variables

        private bool isDuringResizeSequence;
        private bool isGlassShown;
        private bool wasGlassShownBeforeResize;

        // Cache these so we can unsubscribe from events even when the OLV has been disposed.
        private Form myOwner;
        private Form mdiOwner;

        #endregion

    }

    /// <summary>
    /// Wrapper for all native method calls on ListView controls
    /// </summary>
    internal class NativeMethods
    {
        #region Constants

        private const int LVM_FIRST = 0x1000;
        private const int LVM_SETIMAGELIST = LVM_FIRST + 3;
        private const int LVM_SCROLL = LVM_FIRST + 20;
        private const int LVM_GETHEADER = LVM_FIRST + 31;
        private const int LVM_GETCOUNTPERPAGE = LVM_FIRST + 40;
        private const int LVM_SETITEMSTATE = LVM_FIRST + 43;
        private const int LVM_SETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 54;
        private const int LVM_SETITEM = LVM_FIRST + 76;
        private const int LVM_GETTOOLTIPS = 0x1000 + 78;
        private const int LVM_SETTOOLTIPS = 0x1000 + 74;
        private const int LVM_GETCOLUMN = LVM_FIRST + 95;
        private const int LVM_SETCOLUMN = LVM_FIRST + 96;
        private const int LVM_SETSELECTEDCOLUMN = LVM_FIRST + 140;
        private const int LVM_INSERTGROUP = LVM_FIRST + 145;
        private const int LVM_SETGROUPINFO = LVM_FIRST + 147;
        private const int LVM_GETGROUPINFO = LVM_FIRST + 149;
        private const int LVM_GETGROUPSTATE = LVM_FIRST + 92;
        private const int LVM_SETGROUPMETRICS = LVM_FIRST + 155;
        private const int LVM_REMOVEALLGROUPS = LVM_FIRST + 160;

        private const int LVS_EX_SUBITEMIMAGES = 0x0002;

        private const int LVIF_TEXT = 0x0001;
        private const int LVIF_IMAGE = 0x0002;
        private const int LVIF_PARAM = 0x0004;
        private const int LVIF_STATE = 0x0008;
        private const int LVIF_INDENT = 0x0010;
        private const int LVIF_NORECOMPUTE = 0x0800;

        private const int LVCF_FMT = 0x0001;
        private const int LVCF_WIDTH = 0x0002;
        private const int LVCF_TEXT = 0x0004;
        private const int LVCF_SUBITEM = 0x0008;
        private const int LVCF_IMAGE = 0x0010;
        private const int LVCF_ORDER = 0x0020;
        private const int LVCFMT_LEFT = 0x0000;
        private const int LVCFMT_RIGHT = 0x0001;
        private const int LVCFMT_CENTER = 0x0002;
        private const int LVCFMT_JUSTIFYMASK = 0x0003;

        private const int LVCFMT_IMAGE = 0x0800;
        private const int LVCFMT_BITMAP_ON_RIGHT = 0x1000;
        private const int LVCFMT_COL_HAS_IMAGES = 0x8000;

        private const int HDM_FIRST = 0x1200;
        private const int HDM_HITTEST = HDM_FIRST + 6;
        private const int HDM_GETITEMRECT = HDM_FIRST + 7;
        private const int HDM_GETITEM = HDM_FIRST + 11;
        private const int HDM_SETITEM = HDM_FIRST + 12;

        private const int HDI_WIDTH = 0x0001;
        private const int HDI_TEXT = 0x0002;
        private const int HDI_FORMAT = 0x0004;
        private const int HDI_BITMAP = 0x0010;
        private const int HDI_IMAGE = 0x0020;

        private const int HDF_LEFT = 0x0000;
        private const int HDF_RIGHT = 0x0001;
        private const int HDF_CENTER = 0x0002;
        private const int HDF_JUSTIFYMASK = 0x0003;
        private const int HDF_RTLREADING = 0x0004;
        private const int HDF_STRING = 0x4000;
        private const int HDF_BITMAP = 0x2000;
        private const int HDF_BITMAP_ON_RIGHT = 0x1000;
        private const int HDF_IMAGE = 0x0800;
        private const int HDF_SORTUP = 0x0400;
        private const int HDF_SORTDOWN = 0x0200;

        private const int SB_HORZ = 0;
        private const int SB_VERT = 1;
        private const int SB_CTL = 2;
        private const int SB_BOTH = 3;

        private const int SIF_RANGE = 0x0001;
        private const int SIF_PAGE = 0x0002;
        private const int SIF_POS = 0x0004;
        private const int SIF_DISABLENOSCROLL = 0x0008;
        private const int SIF_TRACKPOS = 0x0010;
        private const int SIF_ALL = (SIF_RANGE | SIF_PAGE | SIF_POS | SIF_TRACKPOS);

        private const int ILD_NORMAL = 0x0;
        private const int ILD_TRANSPARENT = 0x1;
        private const int ILD_MASK = 0x10;
        private const int ILD_IMAGE = 0x20;
        private const int ILD_BLEND25 = 0x2;
        private const int ILD_BLEND50 = 0x4;

        const int SWP_NOSIZE = 1;
        const int SWP_NOMOVE = 2;
        const int SWP_NOZORDER = 4;
        const int SWP_NOREDRAW = 8;
        const int SWP_NOACTIVATE = 16;
        public const int SWP_FRAMECHANGED = 32;

        const int SWP_zOrderOnly = SWP_NOSIZE | SWP_NOMOVE | SWP_NOREDRAW | SWP_NOACTIVATE;
        const int SWP_sizeOnly = SWP_NOMOVE | SWP_NOREDRAW | SWP_NOZORDER | SWP_NOACTIVATE;
        const int SWP_updateFrame = SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOZORDER | SWP_FRAMECHANGED;

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct HDITEM
        {
            public int mask;
            public int cxy;
            public IntPtr pszText;
            public IntPtr hbm;
            public int cchTextMax;
            public int fmt;
            public IntPtr lParam;
            public int iImage;
            public int iOrder;
            //if (_WIN32_IE >= 0x0500)
            public int type;
            public IntPtr pvFilter;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class HDHITTESTINFO
        {
            public int pt_x;
            public int pt_y;
            public int flags;
            public int iItem;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class HDLAYOUT
        {
            public IntPtr prc;
            public IntPtr pwpos;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct LVBKIMAGE
        {
            public int ulFlags;
            public IntPtr hBmp;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszImage;
            public int cchImageMax;
            public int xOffset;
            public int yOffset;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct LVCOLUMN
        {
            public int mask;
            public int fmt;
            public int cx;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszText;
            public int cchTextMax;
            public int iSubItem;
            // These are available in Common Controls >= 0x0300
            public int iImage;
            public int iOrder;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct LVFINDINFO
        {
            public int flags;
            public string psz;
            public IntPtr lParam;
            public int ptX;
            public int ptY;
            public int vkDirection;
        }

        [StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct LVGROUP
        {
            public uint cbSize;
            public uint mask;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszHeader;
            public int cchHeader;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszFooter;
            public int cchFooter;
            public int iGroupId;
            public uint stateMask;
            public uint state;
            public uint uAlign;
        }

        [StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct LVGROUP2
        {
            public uint cbSize;
            public uint mask;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszHeader;
            public uint cchHeader;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszFooter;
            public int cchFooter;
            public int iGroupId;
            public uint stateMask;
            public uint state;
            public uint uAlign;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszSubtitle;
            public uint cchSubtitle;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszTask;
            public uint cchTask;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszDescriptionTop;
            public uint cchDescriptionTop;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszDescriptionBottom;
            public uint cchDescriptionBottom;
            public int iTitleImage;
            public int iExtendedImage;
            public int iFirstItem;         // Read only
            public int cItems;             // Read only
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszSubsetTitle;     // NULL if group is not subset
            public uint cchSubsetTitle;
        }

        [StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct LVGROUPMETRICS
        {
            public uint cbSize;
            public uint mask;
            public uint Left;
            public uint Top;
            public uint Right;
            public uint Bottom;
            public int crLeft;
            public int crTop;
            public int crRight;
            public int crBottom;
            public int crHeader;
            public int crFooter;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct LVHITTESTINFO
        {
            public int pt_x;
            public int pt_y;
            public int flags;
            public int iItem;
            public int iSubItem;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct LVITEM
        {
            public int mask;
            public int iItem;
            public int iSubItem;
            public int state;
            public int stateMask;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszText;
            public int cchTextMax;
            public int iImage;
            public IntPtr lParam;
            // These are available in Common Controls >= 0x0300
            public int iIndent;
            // These are available in Common Controls >= 0x056
            public int iGroupId;
            public int cColumns;
            public IntPtr puColumns;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct NMHDR
        {
            public IntPtr hwndFrom;
            public IntPtr idFrom;
            public int code;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NMCUSTOMDRAW
        {
            public NativeMethods.NMHDR nmcd;
            public int dwDrawStage;
            public IntPtr hdc;
            public NativeMethods.RECT rc;
            public IntPtr dwItemSpec;
            public int uItemState;
            public IntPtr lItemlParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NMHEADER
        {
            public NMHDR nhdr;
            public int iItem;
            public int iButton;
            public IntPtr pHDITEM;
        }

        const int MAX_LINKID_TEXT = 48;
        const int L_MAX_URL_LENGTH = 2048 + 32 + 4;
        //#define L_MAX_URL_LENGTH    (2048 + 32 + sizeof("://"))

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LITEM
        {
            public uint mask;
            public int iLink;
            public uint state;
            public uint stateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_LINKID_TEXT)]
            public string szID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = L_MAX_URL_LENGTH)]
            public string szUrl;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NMLISTVIEW
        {
            public NativeMethods.NMHDR hdr;
            public int iItem;
            public int iSubItem;
            public int uNewState;
            public int uOldState;
            public int uChanged;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NMLVCUSTOMDRAW
        {
            public NativeMethods.NMCUSTOMDRAW nmcd;
            public int clrText;
            public int clrTextBk;
            public int iSubItem;
            public int dwItemType;
            public int clrFace;
            public int iIconEffect;
            public int iIconPhase;
            public int iPartId;
            public int iStateId;
            public NativeMethods.RECT rcText;
            public uint uAlign;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NMLVFINDITEM
        {
            public NativeMethods.NMHDR hdr;
            public int iStart;
            public NativeMethods.LVFINDINFO lvfi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NMLVGETINFOTIP
        {
            public NativeMethods.NMHDR hdr;
            public int dwFlags;
            public string pszText;
            public int cchTextMax;
            public int iItem;
            public int iSubItem;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NMLVLINK
        {
            public NMHDR hdr;
            public LITEM link;
            public int iItem;
            public int iSubItem;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NMLVSCROLL
        {
            public NativeMethods.NMHDR hdr;
            public int dx;
            public int dy;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct NMTTDISPINFO
        {
            public NativeMethods.NMHDR hdr;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszText;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szText;
            public IntPtr hinst;
            public int uFlags;
            public IntPtr lParam;
            //public int hbmp; This is documented but doesn't work
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class SCROLLINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(NativeMethods.SCROLLINFO));
            public int fMask;
            public int nMin;
            public int nMax;
            public int nPage;
            public int nPos;
            public int nTrackPos;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class TOOLINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(NativeMethods.TOOLINFO));
            public int uFlags;
            public IntPtr hwnd;
            public IntPtr uId;
            public NativeMethods.RECT rect;
            public IntPtr hinst = IntPtr.Zero;
            public IntPtr lpszText;
            public IntPtr lParam = IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int flags;
        }

        #endregion

        #region Entry points

        // Various flavours of SendMessage: plain vanilla, and passing references to various structures
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, int lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageLVItem(IntPtr hWnd, int msg, int wParam, ref LVITEM lvi);
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, ref LVHITTESTINFO ht);
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageRECT(IntPtr hWnd, int msg, int wParam, ref RECT r);
        //[DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        //private static extern IntPtr SendMessageLVColumn(IntPtr hWnd, int m, int wParam, ref LVCOLUMN lvc);
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageHDItem(IntPtr hWnd, int msg, int wParam, ref HDITEM hdi);
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageHDHITTESTINFO(IntPtr hWnd, int Msg, IntPtr wParam, [In, Out] HDHITTESTINFO lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTOOLINFO(IntPtr hWnd, int Msg, int wParam, NativeMethods.TOOLINFO lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageLVBKIMAGE(IntPtr hWnd, int Msg, int wParam, ref NativeMethods.LVBKIMAGE lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageString(IntPtr hWnd, int Msg, int wParam, string lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageIUnknown(IntPtr hWnd, int msg,
            [MarshalAs(UnmanagedType.IUnknown)] object wParam, int lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, ref LVGROUP lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, ref LVGROUP2 lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, ref LVGROUPMETRICS lParam);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr objectHandle);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool GetClientRect(IntPtr hWnd, ref Rectangle r);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool GetScrollInfo(IntPtr hWnd, int fnBar, SCROLLINFO scrollInfo);

        [DllImport("user32.dll", EntryPoint = "GetUpdateRect", CharSet = CharSet.Auto)]
        private static extern int GetUpdateRectInternal(IntPtr hWnd, ref Rectangle r, bool eraseBackground);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        private static extern bool ImageList_Draw(IntPtr himl, int i, IntPtr hdcDst, int x, int y, int fStyle);

        //[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        //public static extern bool SetScrollInfo(IntPtr hWnd, int fnBar, SCROLLINFO scrollInfo, bool fRedraw);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        public static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        public static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        public static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Auto)]
        public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", EntryPoint = "ValidateRect", CharSet = CharSet.Auto)]
        private static extern IntPtr ValidatedRectInternal(IntPtr hWnd, ref Rectangle r);

        #endregion

        //[DllImport("user32.dll", EntryPoint = "LockWindowUpdate", CharSet = CharSet.Auto)]
        //private static extern int LockWindowUpdateInternal(IntPtr hWnd);

        //public static void LockWindowUpdate(IWin32Window window) {
        //    if (window == null)
        //        NativeMethods.LockWindowUpdateInternal(IntPtr.Zero);
        //    else
        //        NativeMethods.LockWindowUpdateInternal(window.Handle);
        //}

        /// <summary>
        /// Put an image under the ListView.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The ListView must have its handle created before calling this.
        /// </para>
        /// <para>
        /// This doesn't work very well.
        /// </para>
        /// </remarks>
        /// <param name="lv"></param>
        /// <param name="image"></param>
        /// <returns></returns>
        public static bool SetBackgroundImage(ListView lv, Image image)
        {
            const int LVBKIF_SOURCE_NONE = 0x0;
            //const int LVBKIF_SOURCE_HBITMAP = 0x1;
            //const int LVBKIF_SOURCE_URL = 0x2;
            //const int LVBKIF_SOURCE_MASK = 0x3;
            //const int LVBKIF_STYLE_NORMAL = 0x0;
            //const int LVBKIF_STYLE_TILE = 0x10;
            //const int LVBKIF_STYLE_MASK = 0x10;
            //const int LVBKIF_FLAG_TILEOFFSET = 0x100;
            const int LVBKIF_TYPE_WATERMARK = 0x10000000;
            //const int LVBKIF_FLAG_ALPHABLEND = 0x20000000;

            const int LVM_SETBKIMAGE = 0x108A;

            LVBKIMAGE lvbkimage = new LVBKIMAGE();
            Bitmap bm = image as Bitmap;
            if (bm == null)
                lvbkimage.ulFlags = LVBKIF_SOURCE_NONE;
            else
            {
                lvbkimage.hBmp = bm.GetHbitmap();
                lvbkimage.ulFlags = LVBKIF_TYPE_WATERMARK;
            }

            Application.OleRequired();
            IntPtr result = NativeMethods.SendMessageLVBKIMAGE(lv.Handle, LVM_SETBKIMAGE, 0, ref lvbkimage);
            return (result != IntPtr.Zero);
        }

        public static bool DrawImageList(Graphics g, ImageList il, int index, int x, int y, bool isSelected)
        {
            int flags = ILD_TRANSPARENT;
            if (isSelected)
                flags |= ILD_BLEND25;
            bool result = ImageList_Draw(il.Handle, index, g.GetHdc(), x, y, flags);
            g.ReleaseHdc();
            return result;
        }

        /// <summary>
        /// Make sure the ListView has the extended style that says to display subitem images.
        /// </summary>
        /// <remarks>This method must be called after any .NET call that update the extended styles
        /// since they seem to erase this setting.</remarks>
        /// <param name="list">The listview to send a m to</param>
        public static void ForceSubItemImagesExStyle(ListView list)
        {
            SendMessage(list.Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, LVS_EX_SUBITEMIMAGES, LVS_EX_SUBITEMIMAGES);
        }

        /// <summary>
        /// Make sure the ListView has the extended style that says to display subitem images.
        /// </summary>
        /// <remarks>This method must be called after any .NET call that update the extended styles
        /// since they seem to erase this setting.</remarks>
        /// <param name="list">The listview to send a m to</param>
        /// <param name="style"></param>
        /// <param name="styleMask"></param>
        public static void SetExtendedStyle(ListView list, int style, int styleMask)
        {
            SendMessage(list.Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, style, styleMask);
        }

        /// <summary>
        /// Calculates the number of items that can fit vertically in the visible area of a list-view (which
        /// must be in details or list view.
        /// </summary>
        /// <param name="list">The listView</param>
        /// <returns>Number of visible items per page</returns>
        public static int GetCountPerPage(ListView list)
        {
            return (int)SendMessage(list.Handle, LVM_GETCOUNTPERPAGE, 0, 0);
        }
        /// <summary>
        /// For the given item and subitem, make it display the given image
        /// </summary>
        /// <param name="list">The listview to send a m to</param>
        /// <param name="itemIndex">row number (0 based)</param>
        /// <param name="subItemIndex">subitem (0 is the item itself)</param>
        /// <param name="imageIndex">index into the image list</param>
        public static void SetSubItemImage(ListView list, int itemIndex, int subItemIndex, int imageIndex)
        {
            LVITEM lvItem = new LVITEM();
            lvItem.mask = LVIF_IMAGE;
            lvItem.iItem = itemIndex;
            lvItem.iSubItem = subItemIndex;
            lvItem.iImage = imageIndex;
            SendMessageLVItem(list.Handle, LVM_SETITEM, 0, ref lvItem);
        }

        /// <summary>
        /// Setup the given column of the listview to show the given image to the right of the text.
        /// If the image index is -1, any previous image is cleared
        /// </summary>
        /// <param name="list">The listview to send a m to</param>
        /// <param name="columnIndex">Index of the column to modifiy</param>
        /// <param name="order"></param>
        /// <param name="imageIndex">Index into the small image list</param>
        public static void SetColumnImage(ListView list, int columnIndex, SortOrder order, int imageIndex)
        {
            IntPtr hdrCntl = NativeMethods.GetHeaderControl(list);
            if (hdrCntl.ToInt32() == 0)
                return;

            HDITEM item = new HDITEM();
            item.mask = HDI_FORMAT;
            IntPtr result = SendMessageHDItem(hdrCntl, HDM_GETITEM, columnIndex, ref item);

            item.fmt &= ~(HDF_SORTUP | HDF_SORTDOWN | HDF_IMAGE | HDF_BITMAP_ON_RIGHT);

            if (NativeMethods.HasBuiltinSortIndicators())
            {
                if (order == SortOrder.Ascending)
                    item.fmt |= HDF_SORTUP;
                if (order == SortOrder.Descending)
                    item.fmt |= HDF_SORTDOWN;
            }
            else
            {
                item.mask |= HDI_IMAGE;
                item.fmt |= (HDF_IMAGE | HDF_BITMAP_ON_RIGHT);
                item.iImage = imageIndex;
            }

            result = SendMessageHDItem(hdrCntl, HDM_SETITEM, columnIndex, ref item);
        }

        /// <summary>
        /// Does this version of the operating system have builtin sort indicators?
        /// </summary>
        /// <returns>Are there builtin sort indicators</returns>
        /// <remarks>XP and later have these</remarks>
        public static bool HasBuiltinSortIndicators()
        {
            return OSFeature.Feature.GetVersionPresent(OSFeature.Themes) != null;
        }

        /// <summary>
        /// Return the bounds of the update region on the given control.
        /// </summary>
        /// <remarks>The BeginPaint() system call validates the update region, effectively wiping out this information.
        /// So this call has to be made before the BeginPaint() call.</remarks>
        /// <param name="cntl">The control whose update region is be calculated</param>
        /// <returns>A rectangle</returns>
        public static Rectangle GetUpdateRect(Control cntl)
        {
            Rectangle r = new Rectangle();
            GetUpdateRectInternal(cntl.Handle, ref r, false);
            return r;
        }

        /// <summary>
        /// Validate an area of the given control. A validated area will not be repainted at the next redraw.
        /// </summary>
        /// <param name="cntl">The control to be validated</param>
        /// <param name="r">The area of the control to be validated</param>
        public static void ValidateRect(Control cntl, Rectangle r)
        {
            ValidatedRectInternal(cntl.Handle, ref r);
        }

        /// <summary>
        /// Select all rows on the given listview
        /// </summary>
        /// <param name="list">The listview whose items are to be selected</param>
        public static void SelectAllItems(ListView list)
        {
            NativeMethods.SetItemState(list, -1, 2, 2);
        }

        /// <summary>
        /// Deselect all rows on the given listview
        /// </summary>
        /// <param name="list">The listview whose items are to be deselected</param>
        public static void DeselectAllItems(ListView list)
        {
            NativeMethods.SetItemState(list, -1, 2, 0);
        }

        /// <summary>
        /// Set the item state on the given item
        /// </summary>
        /// <param name="list">The listview whose item's state is to be changed</param>
        /// <param name="itemIndex">The index of the item to be changed</param>
        /// <param name="mask">Which bits of the value are to be set?</param>
        /// <param name="value">The value to be set</param>
        public static void SetItemState(ListView list, int itemIndex, int mask, int value)
        {
            LVITEM lvItem = new LVITEM();
            lvItem.stateMask = mask;
            lvItem.state = value;
            SendMessageLVItem(list.Handle, LVM_SETITEMSTATE, itemIndex, ref lvItem);
        }

        /// <summary>
        /// Scroll the given listview by the given deltas
        /// </summary>
        /// <param name="list"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <returns>true if the scroll succeeded</returns>
        public static bool Scroll(ListView list, int dx, int dy)
        {
            return SendMessage(list.Handle, LVM_SCROLL, dx, dy) != IntPtr.Zero;
        }

        /// <summary>
        /// Return the handle to the header control on the given list
        /// </summary>
        /// <param name="list">The listview whose header control is to be returned</param>
        /// <returns>The handle to the header control</returns>
        public static IntPtr GetHeaderControl(ListView list)
        {
            return SendMessage(list.Handle, LVM_GETHEADER, 0, 0);
        }


        /// <summary>
        /// Return the edges of the given column.
        /// </summary>
        /// <param name="lv"></param>
        /// <param name="columnIndex"></param>
        /// <returns>A Point holding the left and right co-ords of the column.
        /// -1 means that the sides could not be retrieved.</returns>
        public static Point GetScrolledColumnSides(ListView lv, int columnIndex)
        {
            IntPtr hdr = NativeMethods.GetHeaderControl(lv);
            if (hdr == IntPtr.Zero)
                return new Point(-1, -1);

            RECT r = new RECT();
            IntPtr result = NativeMethods.SendMessageRECT(hdr, HDM_GETITEMRECT, columnIndex, ref r);
            int scrollH = NativeMethods.GetScrollPosition(lv, true);
            return new Point(r.left - scrollH, r.right - scrollH);
        }

        /// <summary>
        /// Return the index of the column of the header that is under the given point.
        /// Return -1 if no column is under the pt
        /// </summary>
        /// <param name="handle">The list we are interested in</param>
        /// <param name="pt">The client co-ords</param>
        /// <returns>The index of the column under the point, or -1 if no column header is under that point</returns>
        public static int GetColumnUnderPoint(IntPtr handle, Point pt)
        {
            const int HHT_ONHEADER = 2;
            const int HHT_ONDIVIDER = 4;
            return NativeMethods.HeaderControlHitTest(handle, pt, HHT_ONHEADER | HHT_ONDIVIDER);
        }

        private static int HeaderControlHitTest(IntPtr handle, Point pt, int flag)
        {
            HDHITTESTINFO testInfo = new HDHITTESTINFO();
            testInfo.pt_x = pt.X;
            testInfo.pt_y = pt.Y;
            IntPtr result = NativeMethods.SendMessageHDHITTESTINFO(handle, HDM_HITTEST, IntPtr.Zero, testInfo);
            if ((testInfo.flags & flag) != 0)
                return testInfo.iItem;
            else
                return -1;
        }

        /// <summary>
        /// Return the index of the divider under the given point. Return -1 if no divider is under the pt
        /// </summary>
        /// <param name="handle">The list we are interested in</param>
        /// <param name="pt">The client co-ords</param>
        /// <returns>The index of the divider under the point, or -1 if no divider is under that point</returns>
        public static int GetDividerUnderPoint(IntPtr handle, Point pt)
        {
            const int HHT_ONDIVIDER = 4;
            return NativeMethods.HeaderControlHitTest(handle, pt, HHT_ONDIVIDER);
        }

        /// <summary>
        /// Get the scroll position of the given scroll bar
        /// </summary>
        /// <param name="lv"></param>
        /// <param name="horizontalBar"></param>
        /// <returns></returns>
        public static int GetScrollPosition(ListView lv, bool horizontalBar)
        {
            int fnBar = (horizontalBar ? SB_HORZ : SB_VERT);

            SCROLLINFO scrollInfo = new SCROLLINFO();
            scrollInfo.fMask = SIF_POS;
            if (GetScrollInfo(lv.Handle, fnBar, scrollInfo))
                return scrollInfo.nPos;
            else
                return -1;
        }

        /// <summary>
        /// Change the z-order to the window 'toBeMoved' so it appear directly on top of 'reference'
        /// </summary>
        /// <param name="toBeMoved"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        public static bool ChangeZOrder(IWin32Window toBeMoved, IWin32Window reference)
        {
            return NativeMethods.SetWindowPos(toBeMoved.Handle, reference.Handle, 0, 0, 0, 0, SWP_zOrderOnly);
        }

        /// <summary>
        /// Make the given control/window a topmost window
        /// </summary>
        /// <param name="toBeMoved"></param>
        /// <returns></returns>
        public static bool MakeTopMost(IWin32Window toBeMoved)
        {
            IntPtr HWND_TOPMOST = (IntPtr)(-1);
            return NativeMethods.SetWindowPos(toBeMoved.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_zOrderOnly);
        }

        public static bool ChangeSize(IWin32Window toBeMoved, int width, int height)
        {
            return NativeMethods.SetWindowPos(toBeMoved.Handle, IntPtr.Zero, 0, 0, width, height, SWP_sizeOnly);
        }

        /// <summary>
        /// Show the given window without activating it
        /// </summary>
        /// <param name="win">The window to show</param>
        static public void ShowWithoutActivate(IWin32Window win)
        {
            const int SW_SHOWNA = 8;
            NativeMethods.ShowWindow(win.Handle, SW_SHOWNA);
        }

        /// <summary>
        /// Mark the given column as being selected.
        /// </summary>
        /// <param name="objectListView"></param>
        /// <param name="value">The OLVColumn or null to clear</param>
        /// <remarks>
        /// This method works, but it prevents subitems in the given column from having
        /// back colors. 
        /// </remarks>
        static public void SetSelectedColumn(ListView objectListView, ColumnHeader value)
        {
            NativeMethods.SendMessage(objectListView.Handle,
                LVM_SETSELECTEDCOLUMN, (value == null) ? -1 : value.Index, 0);
        }

        static public IntPtr GetTooltipControl(ListView lv)
        {
            return SendMessage(lv.Handle, LVM_GETTOOLTIPS, 0, 0);
        }

        public static int GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 4)
                return (int)GetWindowLong32(hWnd, nIndex);
            else
                return (int)(long)GetWindowLongPtr64(hWnd, nIndex);
        }

        public static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
        {
            if (IntPtr.Size == 4)
                return (int)SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
            else
                return (int)(long)SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int SetBkColor(IntPtr hDC, int clr);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int SetTextColor(IntPtr hDC, int crColor);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [DllImport("uxtheme.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr SetWindowTheme(IntPtr hWnd, string subApp, string subIdList);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool InvalidateRect(IntPtr hWnd, int ignored, bool erase);

        [StructLayout(LayoutKind.Sequential)]
        public struct LVITEMINDEX
        {
            public int iItem;
            public int iGroup;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

    }
}
