import math
import unittest
from portable.core import liquid_height, liquid_segment, sphere_window, parse_codex, migrate_settings
from portable.app import demo_snapshots

class SphereTests(unittest.TestCase):
    def test_area_round_trip_and_polygon(self):
        for fraction in (0,.0001,.01,.1,.25,.5,.75,.9,.99,.9999,1):
            height=liquid_height(fraction);y=1-2*height
            area=(math.acos(y)-y*math.sqrt(max(0,1-y*y)))/math.pi
            self.assertAlmostEqual(area,fraction,places=10)
            points=liquid_segment(fraction)
            polygon=abs(sum(x*points[(i+1)%len(points)][1]-points[(i+1)%len(points)][0]*y for i,(x,y) in enumerate(points)))/2 if points else 0
            self.assertLess(abs(polygon/math.pi-fraction),.00002)
        self.assertGreater(abs(liquid_height(.25)-.25),.01)

    def test_slots_capabilities_and_error_isolation(self):
        values=demo_snapshots()
        self.assertIsNone(sphere_window(values,"codex","five-hour"))
        self.assertEqual(sphere_window(list(reversed(values)),"codex","weekly").window.remaining,68)
        self.assertEqual(sphere_window(values,"claude","five-hour").window.remaining,82)
        values[1].status="fetch_error"
        self.assertEqual(sphere_window(values,"claude","weekly").condition,"Error")
        self.assertEqual(sphere_window(values,"codex","weekly").condition,"Fresh")
        values[0]=parse_codex({"primary_window":{"used_percent":100,"limit_window_seconds":18000},"secondary_window":{"used_percent":32,"limit_window_seconds":604800}})
        self.assertEqual(sphere_window(values,"codex","five-hour").condition,"Exhausted")

    def test_density_normalization(self):
        for raw,expected in [(None,64),("bad",64),(42,64),(80,80),(1000,96),(True,64)]:
            self.assertEqual(migrate_settings({"ballSize":raw})["ballSize"],expected)

if __name__=="__main__":unittest.main()
