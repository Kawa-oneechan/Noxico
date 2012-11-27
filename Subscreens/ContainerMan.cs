using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing;

namespace Noxico
{
	public class ContainerMan
	{
		private static List<Token> containerTokens = new List<Token>();
		private static List<InventoryItem> containerItems = new List<InventoryItem>();

		private static List<Token> playerTokens = new List<Token>();
		private static List<InventoryItem> playerItems = new List<InventoryItem>();

		private static Container container;

		private static UIWindow containerWindow, playerWindow, descriptionWindow;
		private static UIList containerList, playerList;
		private static UILabel description;
		private static UILabel capacity;
		//private static bool onLeft = true;
		private static int indexLeft, indexRight;

		public static void Setup(Container container)
		{
			NoxicoGame.Mode = UserMode.Subscreen;
			NoxicoGame.Subscreen = ContainerMan.Handler;
			Subscreens.FirstDraw = true;

			ContainerMan.container = container;
		}

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var trig = NoxicoGame.KeyTrg;
			var player = NoxicoGame.HostForm.Noxico.Player;

			if (Subscreens.FirstDraw)
			{
				UIManager.Initialize();
				UIManager.Elements.Clear();

				var height = 1;
				containerTokens.Clear();
				containerItems.Clear();
				containerList = null;
				var containerTexts = new List<string>();

				containerWindow = new UIWindow(container.Name) { Left = 1, Top = 1, Width = 39, Height = 2 + height, Background = Color.Black, Foreground = Color.CornflowerBlue };
				containerList = new UIList("", null, containerTexts) { Left = 2, Top = 2, Width = 37, Height = height, Background = Color.Black, Foreground = Color.Gray, Index = indexLeft };
				UIManager.Elements.Add(containerWindow);
				UIManager.Elements.Add(new UILabel("It's empty.") { Left = 3, Top = 2, Width = 36, Height = 1, Background = Color.Black, Foreground = Color.Gray });
				UIManager.Elements.Add(containerList);

				if (container.Token.GetToken("contents").Tokens.Count == 0)
				{
					containerList.Hidden = true;
					containerWindow.Height = 3;
				}
				else
				{
					foreach (var carriedItem in container.Token.GetToken("contents").Tokens)
					{
						var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
						if (find == null)
							continue;
						containerTokens.Add(carriedItem);
						containerItems.Add(find);

						var item = find;
						var itemString = item.ToString(carriedItem);
						if (itemString.Length > 33)
							itemString = item.ToString(carriedItem, false, false);
						if (itemString.Length > 33)
							itemString = itemString.Disemvowel();
						itemString = itemString.PadRight(33);
						containerTexts.Add(itemString);
					}
					height = containerItems.Count;
					if (height > 13)
						height = 13;
					if (indexLeft >= containerItems.Count)
						indexLeft = containerItems.Count - 1;

					containerList.Items = containerTexts;
					containerWindow.Height = 2 + height;
					containerList.Height = height;
				}

				playerTokens.Clear();
				playerItems.Clear();
				playerList = null;
				var playerTexts = new List<string>();

				playerWindow = new UIWindow("Your inventory") { Left = 42, Top = 1, Width = 37, Height = 3, Background = Color.Black, Foreground = Color.Magenta };
				playerList = new UIList("", null, playerTexts) { Left = 43, Top = 2, Width = 35, Height = 1, Background = Color.Black, Foreground = Color.Gray, Index = indexRight };
				UIManager.Elements.Add(playerWindow);
				UIManager.Elements.Add(new UILabel("You are carrying nothing.") { Left = 44, Top = 2, Width = 36, Height = 1, Background = Color.Black, Foreground = Color.Gray });
				UIManager.Elements.Add(playerList);

				if (player.Character.GetToken("items").Tokens.Count == 0)
				{
					playerList.Hidden = true;
					playerWindow.Height = 3;
				}
				else
				{
					foreach (var carriedItem in player.Character.GetToken("items").Tokens)
					{
						var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
						if (find == null)
							continue;
						playerTokens.Add(carriedItem);
						playerItems.Add(find);

						var item = find;
						var itemString = item.ToString(carriedItem);
						if (itemString.Length > 33)
							itemString = item.ToString(carriedItem, false, false);
						if (itemString.Length > 33)
							itemString = itemString.Disemvowel();
						itemString = itemString.PadRight(33);
						if (carriedItem.HasToken("equipped"))
							itemString = itemString.Remove(32) + "W";
						if (carriedItem.Path("cursed/known") != null)
							itemString = itemString.Remove(32) + "C";
						playerTexts.Add(itemString); 
					}
					var height2 = playerItems.Count;
					if (height2 == 0)
						height2 = 1;
					if (height2 > 13)
						height2 = 13;
					if (indexRight >= playerItems.Count)
						indexRight = playerItems.Count - 1;

					if (height2 > height)
						height = height2;

					playerList.Items = playerTexts;
					playerWindow.Height = 2 + height2;
					playerList.Height = height2;
				}

				UIManager.Elements.Add(new UILabel(" Press Enter to store or retrieve the highlighted item.".PadRight(80)) { Left = 0, Top = 24, Width = 79, Height = 1, Background = Color.Black, Foreground = Color.Silver });
				descriptionWindow = new UIWindow(string.Empty) { Left = 2, Top = 17, Width = 76, Height = 6, Background = Color.Black, Foreground = Color.Navy, Title = Color.Silver };
				description = new UILabel("") { Left = 4, Top = 18, Width = 72, Height = 4, Foreground = Color.Silver, Background = Color.Black };
				capacity = new UILabel(player.Character.Carried + "/" + player.Character.Capacity) { Left = 6, Top = 22, Foreground = Color.Silver, Background = Color.Black };
				UIManager.Elements.Add(descriptionWindow);
				UIManager.Elements.Add(description);
				UIManager.Elements.Add(capacity);

				if (containerList != null)
				{
					containerList.Change = (s, e) =>
					{
						if (containerList.Items.Count == 0)
							return;
						indexLeft = containerList.Index;
						var t = containerTokens[containerList.Index];
						var i = containerItems[containerList.Index];
						descriptionWindow.Text = i.ToString(t, false, false);
						description.Text = Toolkit.Wordwrap(i.GetDescription(t), description.Width);
						descriptionWindow.Draw();
						description.Draw();
						capacity.Draw();
						//UIManager.Draw();
					};
					containerList.Enter = (s, e) =>
					{
						if (containerList.Items.Count == 0)
							return; 
						//onLeft = true;
						TryRetrieve(player, containerTokens[containerList.Index], containerItems[containerList.Index]);
						{
							playerItems.Add(containerItems[containerList.Index]);
							playerTokens.Add(containerTokens[containerList.Index]);
							playerList.Items.Add(containerList.Items[containerList.Index]);
							containerItems.RemoveAt(containerList.Index);
							containerTokens.RemoveAt(containerList.Index);
							containerList.Items.RemoveAt(containerList.Index);
							if (containerList.Index >= containerList.Items.Count)
								containerList.Index--;
							if (containerList.Items.Count == 0)
							{
								containerList.Hidden = true;
								keys[(int)Keys.Right] = true;
							}
							else
								containerList.Height = (containerList.Items.Count < 14) ? containerList.Items.Count : 13;
							playerList.Hidden = false; //always the case.
							playerList.Height = (playerList.Items.Count < 14) ? playerList.Items.Count : 13;
							containerWindow.Height = containerList.Height + 2;
							playerWindow.Height = playerList.Height + 2;
							capacity.Text = player.Character.Carried + "/" + player.Character.Capacity;
							UIManager.Draw();
						}
					};
				}
				if (playerList != null)
				{
					playerList.Change = (s, e) =>
					{
						if (playerList.Items.Count == 0)
							return; 
						indexRight = playerList.Index;
						var t = playerTokens[playerList.Index];
						var i = playerItems[playerList.Index];
						descriptionWindow.Text = i.ToString(t, false, false);
						var desc = i.GetDescription(t);
						if (t.Path("cursed/path") != null)
							desc += "\nThis item is cursed and can't be removed.";
						else if (i.HasToken("equipable") && t.HasToken("equipped"))
							desc += "\nThis item is currently equipped.";
						description.Text = Toolkit.Wordwrap(desc, description.Width);
						descriptionWindow.Draw();
						description.Draw();
						capacity.Draw();
						//UIManager.Draw();
					};
					playerList.Enter = (s, e) =>
					{
						if (playerList.Items.Count == 0)
							return; 
						//onLeft = false;
						if (TryStore(player, playerTokens[playerList.Index], playerItems[playerList.Index]))
						{
							containerItems.Add(playerItems[playerList.Index]);
							containerTokens.Add(playerTokens[playerList.Index]);
							containerList.Items.Add(playerList.Items[playerList.Index]);
							playerItems.RemoveAt(playerList.Index);
							playerTokens.RemoveAt(playerList.Index);
							playerList.Items.RemoveAt(playerList.Index);
							if (playerList.Index >= playerList.Items.Count)
								playerList.Index--;
							if (playerList.Items.Count == 0)
							{
								playerList.Hidden = true;
								keys[(int)Keys.Left] = true;
							}
							else
								playerList.Height = (playerList.Items.Count < 14) ? playerList.Items.Count : 13;
							containerList.Hidden = false; //always the case.
							containerList.Height = (containerList.Items.Count < 14) ? containerList.Items.Count : 13;
							containerWindow.Height = containerList.Height + 2;
							playerWindow.Height = playerList.Height + 2;
							capacity.Text = player.Character.Carried + "/" + player.Character.Capacity;
							UIManager.Draw();
						}
					};
				}

				UIManager.Highlight = containerList.Items.Count > 0 ? containerList : playerList;
				UIManager.Draw();
				if (UIManager.Highlight.Change != null)
					UIManager.Highlight.Change(null, null);
				Subscreens.FirstDraw = false;
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Back))
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Immediate = true;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			else if (NoxicoGame.IsKeyDown(KeyBinding.Left))
			{
				NoxicoGame.ClearKeys();
				if (containerList.Items.Count == 0)
					return; 
				UIManager.Highlight = containerList ?? playerList;
				containerList.DrawQuick();
				containerList.Change(null, null);
				playerList.DrawQuick();
				NoxicoGame.Sound.PlaySound("Cursor");
				//UIManager.Draw();
			}
			else if (NoxicoGame.IsKeyDown(KeyBinding.Right))
			{
				NoxicoGame.ClearKeys();
				if (playerList.Items.Count == 0)
					return; 
				UIManager.Highlight = playerList ?? containerList;
				playerList.Change(null, null);
				containerList.DrawQuick();
				playerList.DrawQuick();
				NoxicoGame.Sound.PlaySound("Cursor");
				//UIManager.Draw();
			}
			else
				UIManager.CheckKeys();
		}

		private static void TryRetrieve(BoardChar boardchar, Token token, InventoryItem chosen)
		{
			var inv = boardchar.Character.GetToken("items");
			var con = container.Token.GetToken("contents");
			inv.Tokens.Add(token);
			con.Tokens.Remove(token);
			boardchar.ParentBoard.Redraw();
			boardchar.ParentBoard.Draw();
			boardchar.Character.CheckHasteSlow();
			NoxicoGame.Sound.PlaySound("Get Item");
			//Subscreens.FirstDraw = true;
		}

		private static bool TryStore(BoardChar boardchar, Token token, InventoryItem chosen)
		{
			var inv = boardchar.Character.GetToken("items");
			var con = container.Token.GetToken("contents");
			//TODO: give feedback on error.
			if (token.HasToken("cursed"))
			{
				if (!token.GetToken("cursed").HasToken("known"))
					token.GetToken("cursed").Tokens.Add(new Token() { Name = "known" });
				return false;
			}
			if (token.HasToken("equipped"))
			{
				return false;
			}
			con.Tokens.Add(token);
			inv.Tokens.Remove(token);
			boardchar.ParentBoard.Redraw();
			boardchar.ParentBoard.Draw();
			boardchar.Character.CheckHasteSlow();
			NoxicoGame.Sound.PlaySound("Put Item");
			return true;
		}
	}
}
