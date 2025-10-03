from flask import Flask

app = Flask(__name__)

@app.route('/')
def hello():
    return "OK!"

@app.route('/tree', methods=['GET'])
def get_tree():
    return [
        {"id": 1, "name": "Root", "parent_id": None},
        {"id": 2, "name": "Child 1", "parent_id": 1},
        {"id": 3, "name": "Child 2", "parent_id": 1}
    ]

if __name__ == '__main__':
    app.run(debug=True)
