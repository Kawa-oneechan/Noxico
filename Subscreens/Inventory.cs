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
	public class Inventory
	{
		//TODO: <del>Rewrite to use UIManager</del> Adapt the Drop key to, it's the only one left.
		private static int selection = 0;
		//Split up the Dictionary for easier access to both halves. It was a silly setup anyway.
		private static List<Token> inventoryTokens = new List<Token>();
		private static List<InventoryItem> inventoryItems = new List<InventoryItem>();
		private static UIList itemList;
		private static UILabel howTo, itemDesc;
		private static UILabel capacity;

		private static void TryUse(Character character, Token token, InventoryItem chosen)
		{
			Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			itemList.Enabled = false;
			chosen.Use(character, token);
			Subscreens.Redraw = true;
		}

		private static void TryDrop(BoardChar boardchar, Token token, InventoryItem chosen)
		{
			//Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			itemList.Enabled = false;
			if (token.HasToken("equipped"))
				try
				{
					chosen.Unequip(boardchar.Character, token);
				}
				catch (ItemException x)
				{
					MessageBox.Message(x.Message);
				}
			if (!token.HasToken("equipped"))
			{
				chosen.Drop(boardchar, token);
				NoxicoGame.HostForm.Noxico.CurrentBoard.Update();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
				NoxicoGame.Sound.PlaySound("Put Item");
				MessageBox.Message("Dropped " + chosen.ToString(token, true, true) + ".");
				/*
				Subscreens.PreviousScreen.Clear();
				NoxicoGame.Mode = UserMode.Walkabout;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				Subscreens.FirstDraw = true;
				NoxicoGame.AddMessage("Dropped " + chosen.ToString(token, true, true) + ".");
				*/
			}
		}

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var trig = NoxicoGame.KeyTrg;
			var player = NoxicoGame.HostForm.Noxico.Player;
			if (!player.Character.HasToken("items") || player.Character.GetToken("items").Tokens.Count == 0)
			{
				MessageBox.Message("You are carrying nothing.", true);
				Subscreens.PreviousScreen.Clear();
				Subscreens.FirstDraw = true;
				return;
			}

			if (Subscreens.FirstDraw)
			{
				UIManager.Initialize();
				Subscreens.FirstDraw = false;
				NoxicoGame.ClearKeys();
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;

				inventoryTokens.Clear();
				inventoryItems.Clear();
				var itemTexts = new List<string>();
				foreach (var carriedItem in player.Character.GetToken("items").Tokens)
				{
					var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
					if (find == null)
						continue;
					inventoryTokens.Add(carriedItem);
					inventoryItems.Add(find);

					var item = find;
					var sigils = new List<string>();
					var carried = carriedItem;
					var icon = " ";

					if (item.HasToken("ascii"))
					{
						var color = "Silver";
						if (item.Path("ascii/fore") != null)
							color = item.Path("ascii/fore").Tokens[0].Name;
						if (carriedItem.HasToken("color"))
							color = carriedItem.GetToken("color").Text;
						if (item.ID == "book")
						{
							var cga = new[] { "Black", "DarkBlue", "DarkGreen", "DarkCyan", "DarkRed", "Purple", "Brown", "Silver", "Gray", "Blue", "Green", "Cyan", "Red", "Magenta", "Yellow", "White" };
							color = cga[(int)carriedItem.GetToken("id").Value % cga.Length];
						}
						if (color.Equals("black", StringComparison.InvariantCultureIgnoreCase))
							color = "Gray";
						icon = "<c" + color + ">" + (char)item.Path("ascii/char").Value;
					}

					if (item.HasToken("equipable"))
					{
						var eq = item.GetToken("equipable");
						if (item.HasToken("weapon"))
							sigils.Add("weapon");
						if (eq.HasToken("head"))
							sigils.Add("head");
						if (eq.HasToken("ring"))
							sigils.Add("ring");
						if (eq.HasToken("underpants") || eq.HasToken("undershirt"))
							sigils.Add("undies");
						if (eq.HasToken("shirt") && !eq.HasToken("pants"))
							sigils.Add("shirt");
						if (eq.HasToken("pants") && !eq.HasToken("shirt"))
							sigils.Add("pants");
						if (eq.HasToken("shirt") && eq.HasToken("pants"))
							sigils.Add("suit");
						if (eq.HasToken("shoes"))
							sigils.Add("shoes");
						if (eq.HasToken("jacket"))
							sigils.Add("jacket");
						if (eq.HasToken("cloak"))
							sigils.Add("cloak");
						if (carried.HasToken("equipped"))
							sigils.Add("equipped");
					}
					if (carried.HasToken("unidentified"))
						sigils.Add("unidentified");
					if (item.HasToken("statbonus"))
					{
						foreach (var bonus in item.GetToken("statbonus").Tokens)
						{
							if (bonus.Name == "health")
								sigils.Add(bonus.Value + "HP");
						}
					}
#if DEBUG
					if (carried.HasToken("cursed"))
						sigils.Add(carried.GetToken("cursed").HasToken("known") ? "cursed" : "cursed!");
#else
					if (carried.HasToken("cursed") && carried.GetToken("cursed").HasToken("known"))
						sigils.Add("cursed");
#endif
					var itemString = item.ToString(carried);
					if (itemString.Length > 40)
						itemString = item.ToString(carried, false, false);
					if (itemString.Length > 40)
						itemString = itemString.Disemvowel();
					itemString = itemString.PadRight(40) + "<cBlack> " + icon + "<cDarkSlateGray> " + string.Join(", ", sigils);
					itemTexts.Add(itemString);
				}
				var height = inventoryItems.Count;
				if (height > 13)
					height = 13;
				if (selection >= inventoryItems.Count)
					selection = inventoryItems.Count - 1;

				//descriptionWindow = new UIWindow(string.Empty) { Left = 2, Top = 17, Width = 76, Height = 6, Background = Color.Black, Foreground = Color.Navy, Title = Color.Silver };
				//description = new UILabel("") { Left = 4, Top = 18, Width = 72, Height = 4, Foreground = Color.Silver, Background = Color.Black };

				UIManager.Elements.Add(new UIWindow("Your inventory") { Left = 1, Top = 1, Width = 78, Height = 2 + height, Background = Color.Black, Foreground = Color.Magenta });
				UIManager.Elements.Add(new UIWindow(string.Empty)  { Left = 2, Top = 17, Width = 76, Height = 6, Background = Color.Black, Foreground = Color.Navy, Title = Color.Silver });
				howTo = new UILabel("") { Left = 0, Top = 24, Width = 79, Height = 1, Background = Color.Black, Foreground = Color.Silver };
				itemDesc = new UILabel("") { Left = 4, Top = 18, Width = 77, Height = 4, Foreground = Color.Silver, Background = Color.Black };
				itemList = new UIList("", null, itemTexts) { Left = 2, Top = 2, Width = 58, Height = height, Background = Color.Black, Foreground = Color.Gray, Index = selection };
				itemList.Change = (s, e) =>
				{
					selection = itemList.Index;

					var t = inventoryTokens[itemList.Index];
					var i = inventoryItems[itemList.Index];
					var r = string.Empty;
					var d = i.GetDescription(t);

					d = Toolkit.Wordwrap(d, itemDesc.Width);

					if (i.ID == "book")
						r = "Press Enter to read.";
					else if (i.HasToken("equipable"))
					{
						if (t.HasToken("equipped"))
						{
							if (t.Path("cursed/known") != null)
								r = "Cannot unequip.";
							else
								r = "Press Enter to unequip.";
						}
						else
							r = "Press Enter to equip.";
					}
					else if (i.HasToken("quest"))
						r = "This is a quest key item.";
					else
						r = "Press Enter to try and use.";

					howTo.Text = (' ' + r).PadRight(80);
					itemDesc.Text = d;
					//howTo.Draw();
					//itemDesc.Draw();
					UIManager.Draw();
				};
				itemList.Enter = (s, e) =>
				{
					TryUse(player.Character, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]);
				};
				capacity = new UILabel(player.Character.Carried + "/" + player.Character.Capacity) { Left = 6, Top = 22, Foreground = Color.Silver, Background = Color.Black };
				UIManager.Elements.Add(howTo);
				UIManager.Elements.Add(itemList);
				UIManager.Elements.Add(itemDesc);
				UIManager.Elements.Add(capacity);
				UIManager.Elements.Add(new UIButton("Drop", (s, e) => { TryDrop(player, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]); }) { Left = 70, Top = 21, Width = 6, Height = 1 });
				UIManager.Highlight = itemList;
				itemList.Index = selection;

				UIManager.Draw();
			}

			if (keys[(int)Keys.Escape] || keys[(int)Keys.I])
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Immediate = true;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			else if (keys[(int)Keys.D])
			{
				TryDrop(player, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]);
			}
			else
				UIManager.CheckKeys();
		}
	}

}
