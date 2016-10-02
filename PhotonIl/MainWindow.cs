using System;
using System.Linq;
using Gtk;
using GLib;
using PhotonIl;
using System.Collections.Generic;


public partial class MainWindow: Gtk.Window
{
	public void LoadIlGen(IlGen gen)
	{
		var size = gen.SubExpressions.Entries.Sum (x => x.Value.Count);
		var dict = new Dictionary<Uid,Uid>(size);
		foreach (var kvpair in gen.SubExpressions.Entries) {
			foreach (var id in kvpair.Value) {
				dict [id] = kvpair.Key;
			}
		}

		var rootelems = dict.Values.Where (x => dict.ContainsKey (x) == false).Distinct().ToArray ();
		Action<Gtk.Container, Uid> add = null;
		add = (container, id) => {
			var hb = new HBox();
			container.Add (hb);
			var vb2 = new VBox();
			hb.Add(vb2);

			var tb = new TextView (){LeftMargin = 2, RightMargin = 2};
			var btn = new Button();
			var hb2 = new HBox();
			hb2.Add(btn);
			hb2.Add(tb);
			vb2.PackStart(hb2,false,false,0);
			tb.KeyPressEvent += textViewKeyPress;

			string text = null;;
			var varname = gen.variableName.Get(id);
			if(varname != null){
				var varvalue = gen.variableValue.Get(id);
				if(varvalue != null)
					text = string.Format("{0}({1})", varname, varvalue);
				else
					text = string.Format("{0}", varname);
			}
			if(id == IlGen.CallExpression)
				text = "Call";
			var inv = gen.FunctionName.Get(id);
			if(inv != null)
				text = inv;
			if(text == null)
				text = ">";
			tb.Buffer.Text = text;
			tb.KeyPressEvent += textViewKeyPress;
			//hb.Add (tb);
			var vb = new VBox ();

			hb.Add (vb);
			foreach(var e in gen.SubExpressions.Get(id))
				add(vb, e);
		};
		// now recursively generate HBoxes and VBoxes to match the ASt.
		foreach (var elem in rootelems)
			add (fixed1, elem);
		ShowAll ();
	}

	public MainWindow () : base (Gtk.WindowType.Toplevel)
	{
		Build ();
		// Container child GtkScrolledWindow.Gtk.Container+ContainerChild
		ShowAll ();
	}

	[ConnectBefore]
	void textViewKeyPress(object o, KeyPressEventArgs args){
		if (args.Event.Key == Gdk.Key.Return) {
			(o as TextView).HasFocus = false;
			(o as TextView).ModifyText (StateType.Selected);
			args.RetVal = true;
		}
	}

	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		Application.Quit ();
		a.RetVal = true;
	}
}
