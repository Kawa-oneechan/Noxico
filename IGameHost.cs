//To make it easier to write functional alternative frontends, they must implement this.
namespace Noxico
{
	public interface IGameHost
	{
		NoxicoGame Noxico { get; }

		string IniPath { get; }
	
		/// <summary>
		/// Sets the content of the cell at the given location to the specified character and colors.
		/// The change will be made visible the next time <seealso cref="IGameHost.Draw"/> is invoked.
		/// If the location is out of bounds, nothing is done, in silence.
		/// </summary>
		/// <param name="row">A value from 0 to 24 inclusive specifying the vertical location of the character.</param>
		/// <param name="col">A value from 0 to 79 inclusive specifying the horizontal location of the character.</param>
		/// <param name="character">A character code. Range depends on implementation.</param>
		/// <param name="foregroundColor">A <seealso cref="Noxico.Color"/> specifying the foreground color of the new cell.</param>
		/// <param name="backgroundColor">A <seealso cref="Noxico.Color"/> specifying the background color of the new cell.</param>
		/// <param name="forceRedraw">If true, ensures that the new cell is drawn, even if nothing changed.</param>
		void SetCell(int row, int col, int character, Color foregroundColor, Color backgroundColor, bool forceRedraw = false);
		
		/// <summary>
		/// Clears the entire screen buffer to the given character and <seealso cref="Noxico.Color"/> values.
		/// </summary>
		/// <param name="character">A character code. Range depends on implementation.</param>
		/// <param name="foregroundColor">A <seealso cref="Noxico.Color"/> specifying the foreground color to clear with.</param>
		/// <param name="backgroundColor">A <seealso cref="Noxico.Color"/> specifying the background color to clear with.</param>
		void Clear(char character, Color foregroundColor, Color backgroundColor);
		
		/// <summary>
		/// Clears the entire screen with a default character and color.
		/// </summary>
		/// <remarks>
		/// Preferably, this is done with U+0020 SPACE, in white on black.
		/// </remarks>
		void Clear();
		
		/// <summary>
		/// Draws the screen buffer for immediate display.
		/// </summary>
		/// <remarks>
		/// Preferably, this is optimized by remembering the previous state of the screen buffer so that only changed cells
		/// are drawn. That is what the final parameter of <seealso cref="IGameHost.SetCell"/> is for.
		/// </remarks>
		void Draw();
		
		/// <summary>
		/// Writes a text string to the screen buffer, at the specified location and in the given <seealso cref="Noxico.Color"/> values.
		/// </summary>
		/// <param name="text">The text string to draw.</param>
		/// <param name="foregroundColor">A <seealso cref="Noxico.Color"/> specifying the initial foreground color to write with.</param>
		/// <param name="backgroundColor">A <seealso cref="Noxico.Color"/> specifying the initial background color to write with.</param>
		/// <param name="row">A value from 0 to 24 inclusive specifying the vertical location of the initial character.</param>
		/// <param name="col">A value from 0 to 79 inclusive specifying the horizontal location of the initial character.</param>
		/// <remarks>
		/// The \r escape code (U+000D CARRIAGE RETURN) is ignored. Only \n (U+000A LINE FEED) is used.
		/// To insert arbitrary characters, you can use the &lt;g####&gt; tag, but regular \u#### is preferred.
		/// To change drawing color, use the &lt;cFore,Back&gt; tag.
		/// </remarks>
		void Write(string text, Color foregroundColor, Color backgroundColor, int row = 0, int col = 0, bool darken = false);
		
		/// <summary>
		/// Moves an entire block of cells in the screen buffer up one row, to make space for new stuff below.
		/// </summary>
		/// <param name="topRow">The top row to scroll away.</param>
		/// <param name="bottomRow">The bottom row to scroll up.</param>
		/// <param name="leftCol">The left hand column.</param>
		/// <param name="rightCol">The right hand column.</param>
		/// <param name="reveal">A <seealso cref="Noxico.Color"/> to fill the newly revealed gap with.</param>
		/// <remarks>
		/// This is mostly here to allow <seealso cref="TextScroller"/> to quickly scroll a good screenfull of text
		/// without having to redraw the entire block from scratch.
		/// </remarks>
		void ScrollUp(int topRow, int bottomRow, int leftCol, int rightCol, Color reveal);

		/// <summary>
		/// Moves an entire block of cells in the screen buffer down one row, to make space for new stuff on top.
		/// </summary>
		/// <param name="topRow">The top row to scroll down.</param>
		/// <param name="bottomRow">The bottom row to scroll away.</param>
		/// <param name="leftCol">The left hand column.</param>
		/// <param name="rightCol">The right hand column.</param>
		/// <param name="reveal">A <seealso cref="Noxico.Color"/> to fill the newly revealed gap with.</param>
		/// <remarks>
		/// This is mostly here to allow <seealso cref="TextScroller"/> to quickly scroll a good screenfull of text
		/// without having to redraw the entire block from scratch.
		/// </remarks>
		void ScrollDown(int topRow, int bottomRow, int leftCol, int rightCol, Color reveal);
		
		/// <summary>
		/// Causes the host window to close.
		/// </summary>
		void Close();

		Point Cursor { get; set; }

		void RestartGraphics();

#if DEBUG
		string Text { get; set; }
#endif
	}
}
