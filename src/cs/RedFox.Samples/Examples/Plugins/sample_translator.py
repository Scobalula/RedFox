# ---------------------------------------------------------------------------
# RedFox Python plugin sample.
#
# This script subclasses SceneTranslator from RedFox.Graphics3D.IO and
# registers itself with the SceneTranslatorManager exposed by the host
# application via PluginManager.Services["scene-translators"].
#
# The host calls initialize(plugin) when the script is loaded and
# deinitialize(plugin) before unload. The plugin.Unloading event is the
# preferred hook for tearing down anything created at runtime.
# ---------------------------------------------------------------------------

import clr
import uuid
clr.AddReference("RedFox.Graphics3D")
clr.AddReference("RedFox.Graphics3D.IO")

from System import Array, String
from RedFox.Graphics3D.IO import ScriptableSceneTranslator


_EXTENSIONS = Array[String]([".pytxt"])

# pythonnet allocates a single CLR proxy type per ``__namespace__`` + class
# name pair, and the lookup persists for the lifetime of the process. When
# the plugin manager unloads and re-loads this script, redefining the class
# under the same namespace raises "Duplicate type name within an assembly".
# Uniquify the namespace per import so reload always produces a fresh proxy.
_PLUGIN_NAMESPACE = f"RedFox.Plugins.Samples.G{uuid.uuid4().hex}"


class PyTextTranslator(ScriptableSceneTranslator):
    """A trivial scene writer that dumps node names as UTF-8 text."""

    __namespace__ = _PLUGIN_NAMESPACE

    # pythonnet 3.1 dispatches C# property getters to explicit
    # ``get_<Name>`` / ``set_<Name>`` methods on the Python subclass.
    # Using ``@property`` does not satisfy the proxy and raises
    # ``NotImplementedException: Python object does not have a 'get_Name' method``
    # the first time C# reads the property.

    def get_Name(self):
        return "python-text"

    def get_CanRead(self):
        return False

    def get_CanWrite(self):
        return True

    def get_Extensions(self):
        return _EXTENSIONS

    def Read(self, scene, stream, context, token):
        raise NotImplementedError("python-text is write-only.")

    def Write(self, scene, stream, context, token):
        lines = [f"# python-text dump of '{scene.Name}'"]
        for node in scene.GetDescendants():
            lines.append(f"node {node.Name}")
        payload = ("\n".join(lines) + "\n").encode("utf-8")
        stream.Write(payload, 0, len(payload))


def initialize(plugin):
    manager = plugin.Manager.Services["scene-translators"]
    translator = PyTextTranslator()
    manager.Register(translator)

    translator_name = translator.get_Name()
    plugin.Properties["translator-name"] = translator_name

    def on_unloading(sender, args):
        manager.Unregister(translator_name)
        print(f"[py] '{plugin.Name}' unregistered '{translator_name}'")

    plugin.Unloading += on_unloading
    print(f"[py] '{plugin.Name}' registered translator '{translator_name}'")


def deinitialize(plugin):
    print(f"[py] '{plugin.Name}' deinitialize() complete")
