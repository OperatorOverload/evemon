﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EVEMon.ApiErrorHandling;
using EVEMon.Common;
using EVEMon.Common.Notifications;
using EVEMon.Common.Properties;
using EVEMon.Common.SettingsObjects;
using EVEMon.NotificationWindow;

namespace EVEMon.Controls
{
    /// <summary>
    /// Displays a list of notifications.
    /// </summary>
    public partial class NotificationList : UserControl
    {
        private readonly Color m_warningColor = Color.LightGoldenrodYellow;
        private readonly Color m_errorColor = Color.LavenderBlush;
        private readonly Color m_infoColor = Color.AliceBlue;
        private readonly List<NotificationEventArgs> m_notifications = new List<NotificationEventArgs>();

        private const int TextLeft = 20;
        private const int LeftPadding = 2;
        private const int RightPadding = 2;
        private const int IconDeletePositionFromRight = 14;
        private const int IconMagnifierPositionFromRight = 34;

        private int m_hoveredIndex = -1;

        private bool m_pendingUpdate;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NotificationList()
        {
            InitializeComponent();

            listBox.DrawItem += listBox_DrawItem;
            listBox.MouseMove += listBox_MouseMove;
            listBox.MouseDown += listBox_MouseDown;
            listBox.MouseLeave += listBox_MouseLeave;

            listBox.Font = FontFactory.GetFont("Tahoma", 8.25F);
        }

        /// <summary>
        /// On load, fils up the list for the design mode.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!DesignMode && !this.IsDesignModeHosted())
                return;

            List<NotificationEventArgs> list = new List<NotificationEventArgs>();
            NotificationEventArgs notification = new NotificationEventArgs(null, NotificationCategory.AccountNotInTraining)
                                                     {
                                                         Priority = NotificationPriority.Information,
                                                         Description = "Some information"
                                                     };

            list.Add(notification);

            notification = new NotificationEventArgs(null, NotificationCategory.AccountNotInTraining)
                               { Priority = NotificationPriority.Warning, Description = "Some warning" };
            list.Add(notification);

            notification = new NotificationEventArgs(null, NotificationCategory.AccountNotInTraining)
                               { Priority = NotificationPriority.Error, Description = "Some error" };
            list.Add(notification);

            Notifications = list;
        }

        /// <summary>
        /// Gets or sets the displayed notifications.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public IEnumerable<NotificationEventArgs> Notifications
        {
            get { return m_notifications; }
            set
            {
                m_notifications.Clear();
                if (value != null)
                {
                    IEnumerable<NotificationEventArgs> notificationsToAdd = value.Where(
                        x => Settings.Notifications.Categories[x.Category].ShowOnMainWindow);
                    m_notifications.AddRange(notificationsToAdd.ToArray().OrderBy(x => (int)x.Priority));
                }
                UpdateContent();
            }
        }

        /// <summary>
        /// Calculates the maximum text length of the list with given font.
        /// </summary>
        /// <param name="font"></param>
        /// <returns></returns>
        private int CalculateMaxTextLength(Font font)
        {
            int maxTextLength = 0;

            foreach (Size textSize in listBox.Items.Cast<object>().Select(item => item.ToString()).Select(
                text => TextRenderer.MeasureText(text, font)).Where(textSize => textSize.Width > maxTextLength))
            {
                maxTextLength = textSize.Width;
            }

            return maxTextLength;
        }

        /// <summary>
        /// Calculates the font according to the notifications to display.
        /// </summary>
        private void CalculateFontSize()
        {
            if (Width == 0)
                return;

            Font font = Font;
            float fontSize = font.Size;

            // Check for magnifier icon
            NotificationEventArgs itemWithDetails = listBox.Items.OfType<NotificationEventArgs>().FirstOrDefault(x => x.HasDetails);
            int magnifierIconSize = itemWithDetails != null ? IconMagnifierPositionFromRight : 0;

            // Calculates the available text space
            int availableTextSpace = Width - LeftPadding - TextLeft - magnifierIconSize - IconDeletePositionFromRight -
                                     RightPadding;

            // If any text length exceeds our bounds we decrease the font size
            while ((CalculateMaxTextLength(font) > availableTextSpace) && (fontSize > 6.5f))
            {
                fontSize -= 0.05f;
                font = FontFactory.GetFont("Tahoma", fontSize);
            }

            // If any text length fits better in our bounds we increase the font size
            while ((CalculateMaxTextLength(font) < availableTextSpace) && (fontSize < 8.25f))
            {
                fontSize += 0.05f;
                font = FontFactory.GetFont("Tahoma", fontSize);
            }

            Font = font;
        }

        /// <summary>
        /// Updates the content of the listbox.
        /// </summary>
        private void UpdateContent()
        {
            if (!Visible)
            {
                m_pendingUpdate = true;
                return;
            }

            m_pendingUpdate = false;

            listBox.BeginUpdate();
            try
            {
                listBox.Items.Clear();
                foreach (NotificationEventArgs notification in m_notifications)
                {
                    listBox.Items.Add(notification);
                }
            }
            finally
            {
                listBox.EndUpdate();
            }

            UpdateSize();
        }

        /// <summary>
        /// Updates the size of the control.
        /// </summary>
        private void UpdateSize()
        {
            Height = listBox.Items.Count * listBox.ItemHeight;
            Width = listBox.Width;
            CalculateFontSize();
            Invalidate();
        }

        /// <summary>
        /// Draws an item.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void listBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1)
                return;

            Graphics g = e.Graphics;

            NotificationEventArgs notification = listBox.Items[e.Index] as NotificationEventArgs;
            if (notification == null)
                return;

            // Retrieves the icon and background color
            Image icon;
            Color color;
            switch (notification.Priority)
            {
                case NotificationPriority.Error:
                    icon = Resources.Error16;
                    color = m_errorColor;
                    break;
                case NotificationPriority.Warning:
                    icon = Resources.Warning16;
                    color = m_warningColor;
                    break;
                case NotificationPriority.Information:
                    icon = Resources.Information16;
                    color = m_infoColor;
                    break;
                default:
                    throw new NotImplementedException();
            }

            // Background
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, e.Bounds);
            }

            // Left icon
            g.DrawImageUnscaled(icon,
                                new Point(e.Bounds.Left + LeftPadding, e.Bounds.Top + (listBox.ItemHeight - icon.Height) / 2));

            // Delete icon
            icon = (m_hoveredIndex == e.Index ? Resources.CrossBlack : Resources.CrossGray);
            g.DrawImageUnscaled(icon,
                                new Point(e.Bounds.Right - IconDeletePositionFromRight,
                                          e.Bounds.Top + (listBox.ItemHeight - icon.Height) / 2));

            // Magnifier icon
            if (notification.HasDetails)
            {
                icon = Resources.Magnifier;
                g.DrawImageUnscaled(icon,
                                    new Point(e.Bounds.Right - IconMagnifierPositionFromRight,
                                              e.Bounds.Top + (listBox.ItemHeight - icon.Height) / 2));
            }

            // Text
            using (SolidBrush foreBrush = new SolidBrush(ForeColor))
            {
                string text = notification.ToString();
                Size size = g.MeasureString(text, Font).ToSize();
                g.DrawString(text, Font, foreBrush,
                             new Point(e.Bounds.Left + TextLeft, e.Bounds.Top + (listBox.ItemHeight - size.Height) / 2));
            }

            // Draw line on top
            using (SolidBrush lineBrush = new SolidBrush(Color.Gray))
            {
                using (Pen pen = new Pen(lineBrush, 1.0f))
                {
                    g.DrawLine(pen, new Point(e.Bounds.Left, e.Bounds.Bottom - 1), new Point(e.Bounds.Right, e.Bounds.Bottom - 1));
                }
            }
        }

        /// <summary>
        /// When the mouse moves, detect the hovered index.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBox_MouseMove(object sender, MouseEventArgs e)
        {
            int oldHoveredIndex = m_hoveredIndex;

            m_hoveredIndex = -1;
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                Rectangle rect = GetDeleteIconRect(listBox.GetItemRectangle(i));
                if (!rect.Contains(e.Location))
                    continue;

                // Repaint the listbox if the previous index was different
                m_hoveredIndex = i;
                if (oldHoveredIndex != m_hoveredIndex)
                {
                    listBox.Invalidate();
                    DisplayTooltip((NotificationEventArgs)listBox.Items[i]);
                }
                return;
            }

            toolTip.Active = false;
        }

        /// <summary>
        /// When the user clicks, we need to detect whether it was on one of the buttons.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBox_MouseDown(object sender, MouseEventArgs e)
        {
            // First test whether the "delete" or the "magnifier" icons have been clicked
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                Rectangle rect = listBox.GetItemRectangle(i);
                if (!rect.Contains(e.Location))
                    continue;

                NotificationEventArgs notification = listBox.Items[i] as NotificationEventArgs;

                // Did the click on the "magnifier" icon ?
                if (notification != null && notification.HasDetails)
                {
                    Rectangle magnifierRect = GetMagnifierIconRect(rect);
                    if (magnifierRect.Contains(e.Location))
                    {
                        ShowDetails(notification);
                        return;
                    }
                }

                // Did the click on the "delete" icon or did a wheel-click?
                Rectangle deleteRect = GetDeleteIconRect(rect);
                if (e.Button != MouseButtons.Middle && !deleteRect.Contains(e.Location))
                    continue;

                EveMonClient.Notifications.Invalidate(new NotificationInvalidationEventArgs(notification));
                m_notifications.Remove(notification);
                UpdateContent();
                return;
            }
        }

        /// <summary>
        /// Show the details for the given notification.
        /// </summary>
        /// <param name="notification"></param>
        private static void ShowDetails(NotificationEventArgs notification)
        {
            // API error ?
            APIErrorNotificationEventArgs errorNotification = notification as APIErrorNotificationEventArgs;
            if (errorNotification != null)
            {
                ApiErrorWindow window = WindowsFactory.ShowByTag<ApiErrorWindow, APIErrorNotificationEventArgs>(errorNotification);
                window.Notification = errorNotification;
                return;
            }

            // Skills Completion ?
            SkillCompletionNotificationEventArgs skillNotifications = notification as SkillCompletionNotificationEventArgs;
            if (skillNotifications != null)
            {
                SkillCompletionWindow window =
                    WindowsFactory.ShowByTag<SkillCompletionWindow, SkillCompletionNotificationEventArgs>(skillNotifications);
                window.Notification = skillNotifications;
                return;
            }

            // Claimable certificate ?
            ClaimableCertificateNotificationEventArgs certNotifications =
                notification as ClaimableCertificateNotificationEventArgs;
            if (certNotifications != null)
            {
                ClaimableCertificateWindow window =
                    WindowsFactory.ShowByTag<ClaimableCertificateWindow, ClaimableCertificateNotificationEventArgs>(
                        certNotifications);
                window.Notification = certNotifications;
                return;
            }

            // Market orders ?
            MarketOrdersNotificationEventArgs ordersNotification = notification as MarketOrdersNotificationEventArgs;
            if (ordersNotification != null)
            {
                MarketOrdersWindow window =
                    WindowsFactory.ShowByTag<MarketOrdersWindow, MarketOrdersNotificationEventArgs>(ordersNotification);
                window.Orders = ordersNotification.Orders;
                window.Columns = Settings.UI.MainWindow.MarketOrders.Columns;
                window.Grouping = MarketOrderGrouping.State;
                window.ShowIssuedFor = IssuedFor.All;
                return;
            }

            // Contracts ?
            ContractsNotificationEventArgs contractsNotification = notification as ContractsNotificationEventArgs;
            if (contractsNotification != null)
            {
                ContractsWindow window =
                    WindowsFactory.ShowByTag<ContractsWindow, ContractsNotificationEventArgs>(contractsNotification);
                window.Contracts = contractsNotification.Contracts;
                window.Columns = Settings.UI.MainWindow.Contracts.Columns;
                window.Grouping = ContractGrouping.State;
                window.ShowIssuedFor = IssuedFor.All;
                return;
            }

            // Industry jobs ?
            IndustryJobsNotificationEventArgs jobsNotification = notification as IndustryJobsNotificationEventArgs;
            if (jobsNotification != null)
            {
                IndustryJobsWindow window =
                    WindowsFactory.ShowByTag<IndustryJobsWindow, IndustryJobsNotificationEventArgs>(jobsNotification);
                window.Jobs = jobsNotification.Jobs;
                window.Columns = Settings.UI.MainWindow.IndustryJobs.Columns;
                window.Grouping = IndustryJobGrouping.State;
                window.ShowIssuedFor = IssuedFor.All;
                return;
            }
        }

        /// <summary>
        /// Displays the tooltip for the hovered item.
        /// </summary>
        private void DisplayTooltip(NotificationEventArgs notification)
        {
            // No details ?
            if (!notification.HasDetails)
            {
                SetToolTip(false);
                return;
            }

            // API error ?
            APIErrorNotificationEventArgs errorNotification = notification as APIErrorNotificationEventArgs;
            if (errorNotification != null)
            {
                SetToolTip(true, errorNotification.Result.ErrorMessage);
                return;
            }

            // Skills Completion ?
            SkillCompletionNotificationEventArgs skillNotifications = notification as SkillCompletionNotificationEventArgs;
            if (skillNotifications != null)
            {
                SetToolTip(true, SkillCompletionMessage(skillNotifications));
                return;
            }

            // Claimable certificate ?
            ClaimableCertificateNotificationEventArgs certNotifications =
                notification as ClaimableCertificateNotificationEventArgs;
            if (certNotifications != null)
            {
                SetToolTip(true, CertificateClaimableMessage(certNotifications));
                return;
            }

            // Market orders ?
            MarketOrdersNotificationEventArgs ordersNotification = notification as MarketOrdersNotificationEventArgs;
            if (ordersNotification != null)
            {
                SetToolTip(true, MarketOrdersEndedMessage(ordersNotification));
                return;
            }

            // Contracts ?
            ContractsNotificationEventArgs contractsNotification = notification as ContractsNotificationEventArgs;
            if (contractsNotification != null)
            {
                SetToolTip(true, ContractsEndedMessage(contractsNotification));
                return;
            }

            // Industry jobs ?
            IndustryJobsNotificationEventArgs jobsNotification = notification as IndustryJobsNotificationEventArgs;
            if (jobsNotification != null)
            {
                SetToolTip(true, IndustryJobsCompletedMessage(jobsNotification));
                return;
            }
        }

        /// <summary>
        /// Builds the skill completion message.
        /// </summary>
        /// <param name="skillNotifications">The <see cref="EVEMon.Common.Notifications.SkillCompletionNotificationEventArgs"/> instance containing the event data.</param>
        /// <returns></returns>
        private static String SkillCompletionMessage(SkillCompletionNotificationEventArgs skillNotifications)
        {
            StringBuilder builder = new StringBuilder();
            foreach (QueuedSkill skill in skillNotifications.Skills)
            {
                builder.AppendFormat(CultureConstants.DefaultCulture, "{0} {1} completed.", skill.SkillName,
                                     Skill.GetRomanFromInt(skill.Level)).AppendLine();
            }
            return builder.ToString();
        }

        /// <summary>
        /// Builds the claimable certificates message.
        /// </summary>
        /// <param name="certNotifications">The <see cref="EVEMon.Common.Notifications.ClaimableCertificateNotificationEventArgs"/> instance containing the event data.</param>
        /// <returns></returns>
        private static String CertificateClaimableMessage(ClaimableCertificateNotificationEventArgs certNotifications)
        {
            StringBuilder builder = new StringBuilder();
            foreach (Certificate cert in certNotifications.Certificates)
            {
                builder.AppendFormat(CultureConstants.DefaultCulture,
                                     "{0} {1} is claimable.", cert.Name, cert.Grade).AppendLine();
            }
            return builder.ToString();
        }

        /// <summary>
        /// Builds the ended markets orders message.
        /// </summary>
        /// <param name="ordersNotification">The <see cref="EVEMon.Common.Notifications.MarketOrdersNotificationEventArgs"/> instance containing the event data.</param>
        /// <returns></returns>
        private static String MarketOrdersEndedMessage(MarketOrdersNotificationEventArgs ordersNotification)
        {
            StringBuilder builder = new StringBuilder();
            foreach (IGrouping<OrderState, MarketOrder> orderGroup in ordersNotification.Orders.GroupBy(x => x.State))
            {
                if (builder.Length != 0)
                    builder.AppendLine();
                builder.AppendLine(orderGroup.Key.GetHeader());

                foreach (MarketOrder order in orderGroup.Where(order => order.Item != null))
                {
                    const AbbreviationFormat Format = AbbreviationFormat.AbbreviationSymbols;

                    // Expired :    12k/15k invulnerability fields at Pator V - Tech School
                    // Fulfilled :  15k invulnerability fields at Pator V - Tech School
                    if (order.State == OrderState.Expired)
                        builder.Append(FormatHelper.Format(order.RemainingVolume, Format)).Append("/");

                    builder.Append(FormatHelper.Format(order.InitialVolume, Format)).Append(" ");
                    builder.Append(order.Item.Name).Append(" at ");
                    builder.AppendLine(order.Station.Name);
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Builds the ended contracts message.
        /// </summary>
        /// <param name="contractsNotification">The <see cref="EVEMon.Common.Notifications.ContractsNotificationEventArgs"/> instance containing the event data.</param>
        /// <returns></returns>
        private static String ContractsEndedMessage(ContractsNotificationEventArgs contractsNotification)
        {
            StringBuilder builder = new StringBuilder();
            foreach (IGrouping<ContractState, Contract> contractGroup in contractsNotification.Contracts.GroupBy(x => x.State))
            {
                if (builder.Length != 0)
                    builder.AppendLine();
                builder.AppendLine(contractGroup.Key.GetHeader());

                foreach (Contract contract in contractGroup.Where(contract => !String.IsNullOrEmpty(contract.Issuer)))
                {
                    builder.Append(contract.ContractText).Append(" | ");
                    builder.Append(contract.ContractType).Append(" | ");
                    builder.Append(contract.Status);

                    if (contract.State == ContractState.Finished)
                        builder.Append(" accepted by  ").Append(contract.Acceptor);

                    builder.Append(" at ").AppendLine(contract.StartStation.Name);
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Builds the completed industry jobs message.
        /// </summary>
        /// <param name="jobsNotification">The <see cref="EVEMon.Common.Notifications.IndustryJobsNotificationEventArgs"/> instance containing the event data.</param>
        /// <returns></returns>
        private static String IndustryJobsCompletedMessage(IndustryJobsNotificationEventArgs jobsNotification)
        {
            StringBuilder builder = new StringBuilder();
            foreach (IndustryJob job in jobsNotification.Jobs.Where(job => job.InstalledItem != null))
            {
                builder.Append(job.InstalledItem.Name).Append(" at ");
                builder.AppendFormat(CultureConstants.DefaultCulture, "{0} > {1}",
                    job.SolarSystem.Name, job.Installation).AppendLine();
            }
            return builder.ToString();
        }

        /// <summary>
        /// Sets the tool tip.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="active">if set to <c>true</c> [active].</param>
        private void SetToolTip(bool active, string message = null)
        {
            toolTip.Active = active;
            
            if (active)
                toolTip.SetToolTip(listBox, message);
        }

        /// <summary>
        /// When the mouse leaves the control, we "unhover" the delete icon.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBox_MouseLeave(object sender, EventArgs e)
        {
            if (m_hoveredIndex == -1)
                return;

            m_hoveredIndex = -1;
            listBox.Invalidate();
        }

        /// <summary>
        /// Gets the rectangle of the "magnifier" icon for the listbox item at the given index.
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        private static Rectangle GetMagnifierIconRect(Rectangle rect)
        {
            Bitmap icon = Resources.Magnifier;
            int yOffset = (rect.Height - icon.Height) / 2;
            Rectangle magnifierIconRect = new Rectangle(rect.Right - IconMagnifierPositionFromRight, rect.Top + yOffset,
                                                        icon.Width, icon.Height);
            magnifierIconRect.Inflate(2, 8);
            return magnifierIconRect;
        }

        /// <summary>
        /// Gets the rectangle of the "delete" icon for the listbox item at the given index.
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        private static Rectangle GetDeleteIconRect(Rectangle rect)
        {
            Bitmap icon = Resources.CrossBlack;
            int yOffset = (rect.Height - icon.Height) / 2;
            Rectangle deleteIconRect = new Rectangle(rect.Right - IconDeletePositionFromRight, rect.Top + yOffset, icon.Width,
                                                     icon.Height);
            deleteIconRect.Inflate(2, 8);
            return deleteIconRect;
        }

        /// <summary>
        /// When the control becomes visible again, we check whether there was an update pending.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnVisibleChanged(EventArgs e)
        {
            if (Visible && m_pendingUpdate)
                UpdateContent();

            base.OnVisibleChanged(e);
        }
    }
}