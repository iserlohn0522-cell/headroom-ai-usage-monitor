"""Regression for decorated X11 client coordinates vs WM geometry."""
import re
from types import SimpleNamespace
import unittest
from portable.app import HeadroomApp

class DecoratedWindow:
    def __init__(self,position="120x90+200+200"):
        self.position=position
    def geometry(self,value=None):
        if value is not None:self.position="120x90"+value
        return self.position
    # Deliberately different from WM coordinates, as with X11 decorations.
    def winfo_x(self):return int(re.findall(r"[+-]\d+",self.position)[0])+4
    def winfo_y(self):return int(re.findall(r"[+-]\d+",self.position)[1])+32

class DragTests(unittest.TestCase):
    def test_repeated_drag_ignores_client_decoration_offsets(self):
        app=SimpleNamespace(root=DecoratedWindow(),hide_legend=lambda:None,mode="ball")
        for expected in (220,240,260):
            HeadroomApp.start_drag(app,SimpleNamespace(x_root=500,y_root=400))
            event=SimpleNamespace(x_root=520,y_root=420)
            HeadroomApp.move_drag(app,event)
            HeadroomApp.release_drag(app,event)
            self.assertEqual(app.root.geometry(),f"120x90+{expected}+{expected}")
            self.assertIsNone(app.drag)

    def test_right_bottom_anchors_and_zero_movement(self):
        app=SimpleNamespace(root=DecoratedWindow("120x90-100-100"),hide_legend=lambda:None)
        HeadroomApp.start_drag(app,SimpleNamespace(x_root=500,y_root=400))
        HeadroomApp.move_drag(app,SimpleNamespace(x_root=500,y_root=400))
        self.assertEqual(app.root.geometry(),"120x90-100-100")
        HeadroomApp.move_drag(app,SimpleNamespace(x_root=520,y_root=420))
        self.assertEqual(app.root.geometry(),"120x90-80-80")

if __name__=="__main__":unittest.main()
