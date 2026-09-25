"""Check that every original slide is included once in its topic's page manifest."""
import json
import re
import sys
from pathlib import Path
from zipfile import ZipFile

from PIL import Image

project = Path(__file__).resolve().parents[2]
sources = Path(sys.argv[1]) if len(sys.argv) > 1 else Path.home() / 'OneDrive/Documents/NCST/DiscreetMath'
resources = project / 'Assets/Resources'
mapping = {
    'propositional-logic': ['Prelim_Week 1.pptx'],
    'truth-tables': ['Prelim_Week 2.pptx'],
    'rules-of-inference': ['Prelim_Week 3.pptx', 'Prelim_Week 4.pptx'],
    'formal-informal-proofs': ['Midterm_Week 6.pptx'],
    'direct-indirect-proofs': ['Midterm_Week 7.pptx'],
    'mathematical-induction': ['Midterm_Week 8-9.pptm'],
    'sets': ['Prefinal_Week 11-12.pptx'],
    'functions-relations': ['Prefinal_Week 13.pptx'],
    'graphs': ['Final_Week 16.pptx'],
    'trees': ['Final_Week 17-18.pptx'],
}
all_images = set()
total = 0
for topic, decks in mapping.items():
    expected = []
    for deck in decks:
        filename = 'IT 105_Discrete Structures 1_2nd Sem_' + deck
        with ZipFile(sources / filename) as archive:
            count = sum(bool(re.fullmatch(r'ppt/slides/slide\d+\.xml', name)) for name in archive.namelist())
        expected.extend(f'{filename} — slide {i}' for i in range(1, count + 1))
    pages = json.loads((resources / 'StudyLibrary' / (topic + '.json')).read_text(encoding='utf-8-sig'))['pages']
    assert [page['source'] for page in pages] == expected, topic + ': wrong source order or missing slides'
    for page in pages:
        assert page['image'].startswith('StudyLibrary/' + topic + '/'), topic + ': content from another topic'
        assert page['image'] not in all_images, 'Duplicate source page'
        all_images.add(page['image'])
        with Image.open(resources / (page['image'] + '.png')) as image:
            assert image.width >= 1600 and image.height > 0
            image.verify()
    total += len(pages)
    print(f'{topic}: PASS ({len(pages)} source pages)')
print(f'PASS: {len(mapping)} unique topics, {total} original slides, no missing or cross-topic pages.')
