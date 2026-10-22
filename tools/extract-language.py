"""Compatibility entry point: hash-based extraction has been retired."""
import pathlib
import runpy
runpy.run_path(str(pathlib.Path(__file__).with_name('language-catalog.py')), run_name='__main__')