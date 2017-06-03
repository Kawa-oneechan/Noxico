using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Noxico
{
	public partial class MutaPanel : Form
	{
		private Character SavedSource;
		private List<Token> SavedTarget;

		public MutaPanel()
		{
			InitializeComponent();
			ManualStartup();

			// populate menus
			foreach (Token bp in Character.Bodyplans)
			{
				if (bp.HasToken("beast")) continue;
				var bpname = bp.Text;

				var newSource = new ToolStripMenuItem()
				{
					Name = bpname,
					Size = new Size(225, 22),
					Text = "Generate source " + bpname
				};
				sourceMenu.DropDownItems.AddRange(new ToolStripItem[] { newSource });
				newSource.Click += new EventHandler(GenerateSourceMenuItem_Click);

				var newTarget = new ToolStripMenuItem()
				{
					Name = bpname,
					Size = new Size(225, 22),
					Text = "Generate target as " + bpname
				};
				targetMenu.DropDownItems.AddRange(new ToolStripItem[] { newTarget });
				newTarget.Click += new EventHandler(GenerateTargetMenuItem_Click);
			}
		}

		private void GenerateSourceMenuItem_Click(object sender, EventArgs e)
		{
			// populate source box with generated character of selected bodyplan
			ToolStripMenuItem s = (ToolStripMenuItem)sender;
			var newchar = Character.Generate(s.Name, Gender.RollDice);
			sourceBox.Text = newchar.DumpTokens(newchar.Tokens.ToList(), 0);

			SavedSource = newchar;
			ExecMorphTest();
		}

		private void GenerateTargetMenuItem_Click(object sender, EventArgs e)
		{
			// populate target box with selected bodyplan
			ToolStripMenuItem s = (ToolStripMenuItem)sender;
			var targetPlan = s.Name;
			var targetToken = Character.Bodyplans.FirstOrDefault(x => x.Name == "bodyplan" && x.Text == targetPlan);
			var targetTokens = new List<Token>();
			targetTokens.Add(targetToken);
			targetBox.Text = targetToken.DumpTokens(targetTokens, 0);

			SavedTarget = targetTokens;
			ExecMorphTest();
		}

		private void ExecMorphTest()
		{
			if (SavedSource == null || SavedTarget == null) return;

			// populate deltas box
			var morphDeltas = SavedSource.GetMorphDeltas(SavedTarget[0].Text, Gender.Invisible);
			deltasBox.Text = SavedSource.DumpTokens(morphDeltas, 0);

			// pop results.box
			var morphFeedback = SavedSource.Morph(SavedTarget[0].Text);
			resultBox.Text = SavedSource.DumpTokens(SavedSource.Tokens.ToList(), 0);

			// pop feedback box
			messagesBox.Text = morphFeedback;
		}

		// since we're not loading the game normally, I gotta do a lot of stuff manually
		private void ManualStartup()
		{
			// load files
			Mix.Initialize("Noxico");

			// load biomes
			BiomeData.LoadBiomes();

			// bindings
			NoxicoGame.KeyBindings = new Dictionary<KeyBinding, Keys>();
			NoxicoGame.RawBindings = new Dictionary<KeyBinding, string>();
			var keyNames = Enum.GetNames(typeof(Keys)).Select(x => x.ToUpperInvariant());
			//Keep this array in synch with KeyBinding.
			var defaults = new[]
			{
				Keys.Left, Keys.Right, Keys.Up, Keys.Down,
				Keys.OemPeriod, Keys.Enter, Keys.OemQuotes, Keys.OemQuestion,
				Keys.Oemcomma, Keys.OemSemicolon, Keys.Enter, Keys.Escape,
				Keys.F1, Keys.F12, Keys.Tab,
				Keys.Up, Keys.Down
			};
			for (var i = 0; i < defaults.Length; i++)
			{
				var iniKey = ((KeyBinding)i).ToString().ToLowerInvariant();
				var iniValue = IniFile.GetValue("keymap", iniKey, Enum.GetName(typeof(Keys), defaults[i])).ToUpperInvariant();
				if (keyNames.Contains(iniValue))
				{
					NoxicoGame.KeyBindings[(KeyBinding)i] = (Keys)Enum.Parse(typeof(Keys), iniValue, true);
				}
				else
				{
					//Try unfriendly name
					iniValue = "OEM" + iniValue;
					if (keyNames.Contains(iniValue))
						NoxicoGame.KeyBindings[(KeyBinding)i] = (Keys)Enum.Parse(typeof(Keys), iniValue, true);
					else
					{
						//Give up, use default
						NoxicoGame.KeyBindings[(KeyBinding)i] = defaults[i];
						iniValue = defaults[i].ToString().ToUpperInvariant();
					}
				}
				NoxicoGame.RawBindings[(KeyBinding)i] = iniValue;
			}

			// load items
			var items = Mix.GetTokenTree("items.tml");
			NoxicoGame.KnownItems = new List<InventoryItem>();
			foreach (var item in items.Where(t => t.Name == "item" && !t.HasToken("disabled")))
				NoxicoGame.KnownItems.Add(InventoryItem.FromToken(item));
			// load bodyplans and hashes
			NoxicoGame.BodyplanHashes = new Dictionary<string, string>();
			Character.Bodyplans = Mix.GetTokenTree("bodyplans.tml");
			foreach (var bodyPlan in Character.Bodyplans.Where(t => t.Name == "bodyplan"))
			{
				var id = bodyPlan.Text;
				var plan = bodyPlan.Tokens;
				Toolkit.VerifyBodyplan(bodyPlan, id);
				if (bodyPlan.HasToken("beast"))
					continue;
				NoxicoGame.BodyplanHashes.Add(id, Toolkit.GetBodyComparisonHash(bodyPlan));
			}
		}
	}
}